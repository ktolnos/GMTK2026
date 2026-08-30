using System;
using System.Collections.Generic;
using System.IO;

namespace Chronomancers.Sim
{
    /// <summary>
    /// The whole history: every body's spans.
    /// <para>
    /// Undo is layered. A layer groups one takeover together with every claim and divergence it
    /// cascaded, and popping it truncates all of them at once. Because layers pop LIFO, a layer's
    /// spans are always a suffix of the recording order, so undo is a truncation rather than a
    /// rewrite — cheap enough to keep the entire undo stack rather than the two copies the design
    /// originally called for.
    /// </para>
    /// <para>
    /// There is no event log. The one thing a log would have carried that is single-valued — a body's
    /// origin, what released it and when — fits on the body itself. Everything else is detected rather
    /// than recorded: the component that owns a channel notices a change it cannot account for and
    /// diverges, which is the same mechanism whether the unaccountable thing is an HP drop with no
    /// damage dealer, a position inside a door that is now shut, or a push from something that is no
    /// longer there.
    /// </para>
    /// </summary>
    public sealed class Timeline
    {
        readonly Dictionary<SimId, BodyTimeline> _bodies = new();
        readonly List<SimId> _scratchIds = new();
        readonly ChannelRegistry _registry;

        int _seq;
        int _layer;

        public Timeline(ChannelRegistry registry = null) => _registry = registry ?? new ChannelRegistry();

        public ChannelRegistry Registry => _registry;
        public int CurrentLayer => _layer;
        public int CurrentSeq => _seq;
        public int BodyCount => _bodies.Count;
        public IEnumerable<BodyTimeline> Bodies => _bodies.Values;

        /// <summary>
        /// Starts a new undo layer. Only taking control of a character opens one — it is the only
        /// action that erases something rewinding cannot restore.
        /// </summary>
        public int OpenLayer() => ++_layer;

        public BodyTimeline Body(SimId id)
        {
            if (!id.IsValid) throw new ArgumentException("SimId.None has no timeline", nameof(id));
            if (!_bodies.TryGetValue(id, out var body)) _bodies.Add(id, body = new BodyTimeline(id));
            return body;
        }

        public bool TryGetBody(SimId id, out BodyTimeline body) => _bodies.TryGetValue(id, out body);

        // ------------------------------------------------------------------ recording

        /// <summary>
        /// Claims <paramref name="id"/> into recording from <paramref name="at"/> onward, in the
        /// cursor's current direction. Player takeover, contact with something already recording, and
        /// any component finding its recorded state unaccountable all route through here — there is
        /// only one mechanism, and it is also the only repair. A claimed body without a controller is
        /// inert: no intent, but it keeps its momentum, still coasts, is still pushed, and still dies.
        /// <para>
        /// Destruction is not a separate operation. A body goes out of the world by recording samples
        /// that read <see cref="Form.Latent"/>, which outrank an older span saying otherwise by
        /// <see cref="Span.Seq"/> like any other re-recording (rule 7).
        /// </para>
        /// </summary>
        public int Claim(SimId id, int dir, LoopTime at)
        {
            var seq = ++_seq;
            Body(id).BeginSpan(seq, _layer, dir, at);
            return seq;
        }

        public void EndSpan(SimId id) => Body(id).EndSpan();

        /// <summary>Records how to instantiate a body. See <see cref="BodyTimeline.Archetype"/>.</summary>
        public void Declare(SimId id, int archetype) => Body(id).Declare(archetype);

        /// <summary>Records what released a body. See <see cref="BodyTimeline.Origin"/>.</summary>
        public void DeclareOrigin(SimId id, SimId origin, LoopTime at) => Body(id).DeclareOrigin(origin, at);

        // ------------------------------------------------------------------ playback

        public bool Exists(SimId id, LoopTime at) => _bodies.TryGetValue(id, out var body) && body.Exists(at);

        /// <summary>
        /// Every body in the world at <paramref name="at"/>, ascending by id.
        /// <para>
        /// This is the entire input to materialization: the live set is <i>derived</i> from the
        /// timeline each step and reconciled against what is currently instantiated — never
        /// accumulated from spawn and destroy events. That is what makes an arbitrary cursor jump
        /// (rewinding, undo, loading a save) take the same code path as a single forward step, and it
        /// is why a body destroyed at one instant reappears by itself when the cursor descends past
        /// that instant again.
        /// </para>
        /// <para>
        /// Whether the caller satisfies a materialization from a pool or from <c>Instantiate</c> is
        /// invisible here — pooling is a throughput optimisation, not part of correctness.
        /// </para>
        /// </summary>
        public void CollectExisting(LoopTime at, List<SimId> into)
        {
            into.Clear();
            foreach (var pair in _bodies)
                if (pair.Value.Exists(at))
                    into.Add(pair.Key);

            // Sorted so materialization order — and hence any physics that depends on it — does not
            // ride on dictionary iteration order.
            into.Sort(static (a, b) => a.Value.CompareTo(b.Value));
        }

        /// <summary>
        /// The next instant beyond <paramref name="at"/>, travelling in <paramref name="dir"/>, at
        /// which <i>any</i> body's applied state changes — capped at <paramref name="limit"/>, the
        /// instant this frame is heading for.
        /// <para>
        /// The sim loop walks these in turn instead of jumping straight to <paramref name="limit"/>,
        /// so every recorded state is applied exactly once however fast the cursor is moving, and no
        /// divergence check is ever skipped. Playback bodies run no physics — only queries — so this
        /// stays affordable at sane cursor speeds.
        /// </para>
        /// <para>
        /// The sequence is the <i>union</i> across bodies, deliberately. Checks ask what is solid
        /// where a body is, which needs every other body positioned at the same instant; letting a
        /// densely sampled body sub-step alone would compare it against everyone else's stale pose.
        /// </para>
        /// </summary>
        public bool TryNextChange(LoopTime at, int dir, LoopTime limit, out LoopTime next)
        {
            var best = 0;
            var found = false;

            foreach (var body in _bodies.Values)
            {
                if (!body.TryNextChange(at, dir, out var candidate)) continue;
                if (dir > 0 ? candidate.Raw > limit.Raw : candidate.Raw < limit.Raw) continue;
                if (found && (dir > 0 ? candidate.Raw >= best : candidate.Raw <= best)) continue;
                best = candidate.Raw;
                found = true;
            }

            next = LoopTime.FromRaw(best);
            return found;
        }

        /// <summary>
        /// Whether this body was legitimately let go: if something released it, that something still
        /// exists at the instant it did.
        /// <para>
        /// False is a divergence trigger — the release could not have happened, so the body is claimed
        /// and goes back to being latent (rule 8). This is the only check answerable from the timeline
        /// alone. Everything else — a position now inside a shut door, an HP drop with no damage dealer,
        /// a push from something no longer there — belongs to the component that owns the channel, and
        /// takes the same action.
        /// </para>
        /// <para>
        /// Existence of the origin is <i>necessary but never sufficient</i>. Whether the release
        /// actually happened is spatial, and belongs to whatever did the releasing: a character that
        /// still exists but never walked into the reversal machine passes this check.
        /// </para>
        /// </summary>
        public bool OriginHolds(SimId id)
        {
            if (!_bodies.TryGetValue(id, out var body)) return true;
            return !body.Origin.IsValid || Exists(body.Origin, body.OriginAt);
        }

        /// <summary>
        /// Every body whose release no longer has anything behind it, ascending by id.
        /// <para>
        /// Note there is no instant parameter: an origin is a single instant recorded on the body, so
        /// whether it still holds does not depend on where the cursor is.
        /// </para>
        /// </summary>
        public void CollectBrokenOrigins(List<SimId> into)
        {
            into.Clear();
            foreach (var pair in _bodies)
                if (!OriginHolds(pair.Key))
                    into.Add(pair.Key);

            into.Sort(static (a, b) => a.Value.CompareTo(b.Value));
        }

        // ------------------------------------------------------------------ undo

        /// <summary>
        /// Undoes <paramref name="layerId"/> and everything after it. Note that
        /// <see cref="CurrentSeq"/> is deliberately not rewound: a later re-recording must never reuse
        /// a retired span's Seq.
        /// </summary>
        public void PopLayer(int layerId)
        {
            if (layerId <= 0) throw new ArgumentOutOfRangeException(nameof(layerId), layerId, "layers start at 1");

            foreach (var body in _bodies.Values) body.TruncateFromLayer(layerId);

            // A body first touched by the undone layer is left with no spans at all. Drop it, so that
            // undoing really restores the prior state rather than leaving an empty husk behind;
            // Body() recreates it for free if it is ever touched again.
            _scratchIds.Clear();
            foreach (var pair in _bodies)
                if (pair.Value.SpanCount == 0)
                    _scratchIds.Add(pair.Key);
            foreach (var id in _scratchIds) _bodies.Remove(id);

            _layer = layerId - 1;
        }

        // ------------------------------------------------------------------ serialization

        public void WriteTo(BinaryWriter w)
        {
            w.Write(_seq);
            w.Write(_layer);

            w.Write(_bodies.Count);
            foreach (var pair in _bodies)
            {
                w.Write(pair.Key.Value);
                pair.Value.WriteTo(w);
            }
        }

        public void ReadFrom(BinaryReader r)
        {
            _bodies.Clear();

            _seq = r.ReadInt32();
            _layer = r.ReadInt32();

            var bodyCount = r.ReadInt32();
            for (var i = 0; i < bodyCount; i++)
            {
                var id = new SimId(r.ReadInt32());
                var body = new BodyTimeline(id);
                body.ReadFrom(r, _registry);
                _bodies.Add(id, body);
            }
        }
    }
}
