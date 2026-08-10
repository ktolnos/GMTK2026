using System;
using System.Collections.Generic;
using System.IO;

namespace Chronomancers.Sim
{
    /// <summary>
    /// Why a body's recorded outcome no longer adds up. The two failures want opposite repairs, so the
    /// caller has to know which one it is found.
    /// </summary>
    public enum CausalityBreak : byte
    {
        /// <summary>It still adds up.</summary>
        None = 0,

        /// <summary>
        /// Whatever spawned this body no longer exists at the instant it did. <b>Void the body</b>:
        /// something with no cause never happened, so there is nothing to re-simulate.
        /// </summary>
        OriginGone = 1,

        /// <summary>
        /// Whatever destroyed this body no longer exists at the instant it did. <b>Claim the body</b>:
        /// being un-killed leaves something that has to live on from here, so it revives inert.
        /// </summary>
        CauseGone = 2,
    }

    /// <summary>One flagged body and the repair it needs. See <see cref="CausalityBreak"/>.</summary>
    public readonly struct BrokenCausality
    {
        public readonly SimId Id;
        public readonly CausalityBreak Reason;

        public BrokenCausality(SimId id, CausalityBreak reason)
        {
            Id = id;
            Reason = reason;
        }

        public override string ToString() => $"{Id} {Reason}";
    }

    /// <summary>
    /// The whole history: every body's spans.
    /// <para>
    /// Undo is layered. A layer groups one takeover together with every claim, void and divergence it
    /// cascaded, and popping it truncates all of them at once. Because layers pop LIFO, a layer's
    /// spans are always a suffix of the recording order, so undo is a truncation rather than a
    /// rewrite — cheap enough to keep the entire undo stack rather than the two copies the design
    /// originally called for.
    /// </para>
    /// <para>
    /// There is no event log. Everything a log would have carried is single-valued and therefore fits
    /// on the thing it describes: a body has exactly one origin, and a destruction has exactly one
    /// cause. Anything genuinely per-instant — "this HP drop came from that bullet" — is instead
    /// detected by the component that owns the channel, which notices a recorded change it cannot
    /// account for and diverges. That is the same mechanism as a body finding its recorded position
    /// inside a door that is now shut.
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
        /// only one mechanism. A claimed body without a controller is inert: no intent, but it keeps
        /// its momentum, still coasts, is still pushed, and still dies.
        /// </summary>
        public int Claim(SimId id, int dir, LoopTime at) => BeginSpan(id, dir, SpanKind.Recorded, at);

        /// <summary>Destroys <paramref name="id"/> from <paramref name="at"/> onward, with no cause.</summary>
        public int Void(SimId id, int dir, LoopTime at) => Void(id, dir, at, SimId.None, at);

        /// <summary>
        /// Destroys <paramref name="id"/> from <paramref name="at"/> onward, growing with the cursor
        /// via <see cref="BodyTimeline.Extend"/>. Overrides older recordings, which is how a death
        /// propagates into loop time the body used to occupy.
        /// <para>
        /// <paramref name="causedBy"/> is what did it, at <paramref name="causedAt"/> — normally one
        /// unit before <paramref name="at"/>, since a body exists at the instant it is destroyed. Pass
        /// <see cref="SimId.None"/> for an uncaused destruction.
        /// </para>
        /// </summary>
        public int Void(SimId id, int dir, LoopTime at, SimId causedBy, LoopTime causedAt)
        {
            var seq = BeginSpan(id, dir, SpanKind.Void, at);
            if (causedBy.IsValid) Body(id).SetVoidCause(causedBy, causedAt);
            return seq;
        }

        int BeginSpan(SimId id, int dir, SpanKind kind, LoopTime at)
        {
            var seq = ++_seq;
            Body(id).BeginSpan(seq, _layer, dir, kind, at);
            return seq;
        }

        public void EndSpan(SimId id) => Body(id).EndSpan();

        /// <summary>Records how to instantiate a body. See <see cref="BodyTimeline.Archetype"/>.</summary>
        public void Declare(SimId id, int archetype) => Body(id).Declare(archetype);

        /// <summary>Records what spawned a body. See <see cref="BodyTimeline.Origin"/>.</summary>
        public void DeclareOrigin(SimId id, SimId origin, LoopTime at) => Body(id).DeclareOrigin(origin, at);

        // ------------------------------------------------------------------ playback

        public bool Exists(SimId id, LoopTime at) => _bodies.TryGetValue(id, out var body) && body.Exists(at);

        /// <summary>
        /// Every body that exists at <paramref name="at"/>, ascending by id.
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
        /// Whether the timeline's account of this body at <paramref name="at"/> still hangs together:
        /// if it was spawned, whatever spawned it still exists at its origin instant; if it was
        /// destroyed here, whatever destroyed it still exists at that instant.
        /// <para>
        /// False is a divergence trigger — the recorded outcome could not have happened, so the body
        /// must be claimed and re-simulated. Only the checks that are answerable from the timeline
        /// alone live here; everything else (a position now inside a shut door, an HP drop with no
        /// damage dealer to be found) is the owning component's job, and takes the same action.
        /// </para>
        /// </summary>
        public bool CausalityHolds(SimId id, LoopTime at) => Check(id, at) == CausalityBreak.None;

        /// <summary>
        /// <see cref="CausalityHolds"/> with the reason, since a broken origin and a broken cause need
        /// opposite repairs.
        /// <para>
        /// Note that a broken origin is reported in preference to a broken cause: a body that was never
        /// spawned must not be revived, so there is no point asking how it died.
        /// </para>
        /// <para>
        /// Existence of the other party is <i>necessary but never sufficient</i>. It is the part
        /// answerable from the timeline alone; whether the spawn actually happened is spatial, and
        /// belongs to the spawning component. A character that still exists but never walked into the
        /// reversal machine passes this check.
        /// </para>
        /// </summary>
        public CausalityBreak Check(SimId id, LoopTime at)
        {
            if (!_bodies.TryGetValue(id, out var body)) return CausalityBreak.None;

            if (body.Origin.IsValid && !Exists(body.Origin, body.OriginAt)) return CausalityBreak.OriginGone;

            var spanIndex = body.Resolve(at);
            if (spanIndex < 0) return CausalityBreak.None;

            var span = body.GetSpan(spanIndex);
            if (span.Kind != SpanKind.Void || !span.CausedBy.IsValid) return CausalityBreak.None;
            return Exists(span.CausedBy, span.CausedAt) ? CausalityBreak.None : CausalityBreak.CauseGone;
        }

        /// <summary>
        /// Every body whose recorded outcome at <paramref name="at"/> no longer adds up, ascending by
        /// id.
        /// <para>
        /// The two failures want opposite repairs. A spawn whose origin is gone is <b>voided</b> —
        /// something with no cause never happened, so there is nothing to re-simulate, and the void is
        /// an ordinary span outranking the recording it covers. A destruction whose cause is gone is
        /// <b>claimed</b> — the body has to live on from there, so it revives inert.
        /// </para>
        /// </summary>
        public void CollectBrokenCausality(LoopTime at, List<BrokenCausality> into)
        {
            into.Clear();
            foreach (var pair in _bodies)
            {
                var reason = Check(pair.Key, at);
                if (reason != CausalityBreak.None) into.Add(new BrokenCausality(pair.Key, reason));
            }

            into.Sort(static (a, b) => a.Id.Value.CompareTo(b.Id.Value));
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
