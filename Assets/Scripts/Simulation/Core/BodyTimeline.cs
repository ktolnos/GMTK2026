using System;
using System.Collections.Generic;
using System.IO;

namespace Chronomancers.Sim
{
    /// <summary>
    /// Everything ever recorded about one body, across every take.
    /// <para>
    /// Samples are stored once per physics step of whichever body was recording, keyed by the loop
    /// time that step landed on. Density is therefore emergent and inversely proportional to the
    /// body's rate: a bullet-time character stores several samples per loop-second and so replays
    /// at full fidelity no matter who is watching it. Nothing is ever decimated, and reading at a
    /// faster rate simply skips samples — which is what the bracketing search does anyway.
    /// </para>
    /// </summary>
    public sealed class BodyTimeline
    {
        /// <summary>Reserved id for the sample-time index itself.</summary>
        public const int TimesChannelId = -1;

        readonly List<Span> _spans = new();
        readonly ChannelBuffer<LoopTime> _times = new(TimesChannelId);
        readonly Dictionary<int, IChannelBuffer> _channels = new();

        int _active = -1;

        public BodyTimeline(SimId body) => Body = body;

        public SimId Body { get; }

        /// <summary>
        /// What to instantiate to bring this body into the world, as an index into the game's
        /// archetype registry. Zero means an authored scene object: it has no prefab, so it is
        /// deactivated rather than destroyed and re-created.
        /// <para>
        /// Recorded per body rather than inferred, because loading a save has to be able to
        /// materialize a body from the file alone. The registry's indices must therefore stay
        /// stable across builds, or old saves break.
        /// </para>
        /// </summary>
        public int Archetype { get; private set; }

        /// <summary>
        /// What spawned this body, and when. <see cref="SimId.None"/> for an authored scene body.
        /// <para>
        /// A body has exactly one origin, so this belongs here rather than needing a separate event.
        /// The spawn is legitimate only while the origin still exists at <see cref="OriginAt"/> —
        /// otherwise it could not have happened, and the body must be voided.
        /// </para>
        /// </summary>
        public SimId Origin { get; private set; }

        public LoopTime OriginAt { get; private set; }

        public int SpanCount => _spans.Count;
        public int SampleCount => _times.Count;
        public bool IsRecording => _active >= 0;
        public IReadOnlyDictionary<int, IChannelBuffer> Channels => _channels;

        public Span GetSpan(int index) => _spans[index];
        public LoopTime TimeOf(int sampleIndex) => _times[sampleIndex];

        /// <summary><see cref="Span.Seq"/> of the span currently being recorded, or 0.</summary>
        public int ActiveSeq => _active >= 0 ? _spans[_active].Seq : 0;

        /// <summary>Records how to instantiate this body. Idempotent; conflicting values throw.</summary>
        public void Declare(int archetype)
        {
            if (Archetype != 0 && Archetype != archetype)
                throw new InvalidOperationException(
                    $"{Body} is archetype {Archetype}; cannot redeclare it as {archetype}");
            Archetype = archetype;
        }

        /// <summary>Records what spawned this body. Idempotent; conflicting values throw.</summary>
        public void DeclareOrigin(SimId origin, LoopTime at)
        {
            if (Origin.IsValid && (Origin != origin || OriginAt != at))
                throw new InvalidOperationException(
                    $"{Body} already originates from {Origin} at {OriginAt}; cannot redeclare as {origin} at {at}");
            Origin = origin;
            OriginAt = at;
        }

        /// <summary>Records what destroyed the body, on the void span currently being written.</summary>
        public void SetVoidCause(SimId causedBy, LoopTime causedAt)
        {
            var span = ActiveSpanOrThrow();
            if (span.Kind != SpanKind.Void)
                throw new InvalidOperationException($"{Body}: only a void span has a cause");
            span.CausedBy = causedBy;
            span.CausedAt = causedAt;
            _spans[_active] = span;
        }

        public ChannelBuffer<TState> Channel<TState>(int channelId) where TState : unmanaged
        {
            if (_channels.TryGetValue(channelId, out var existing))
                return existing as ChannelBuffer<TState>
                       ?? throw new InvalidOperationException(
                           $"{Body} channel {channelId} holds {existing.StateType.Name}, not {typeof(TState).Name}");

            var created = new ChannelBuffer<TState>(channelId);
            created.Grow(_times.Count); // registered mid-take: back-fill with default
            _channels.Add(channelId, created);
            return created;
        }

        // ------------------------------------------------------------------ recording

        public void BeginSpan(int seq, int layerId, int dir, SpanKind kind, LoopTime at)
        {
            if (_active >= 0)
                throw new InvalidOperationException($"{Body} is already recording ({_spans[_active]})");
            if (dir != 1 && dir != -1)
                throw new ArgumentOutOfRangeException(nameof(dir), dir, "direction must be +1 or -1");

            _spans.Add(new Span
            {
                Seq = seq,
                LayerId = layerId,
                Dir = (sbyte)dir,
                Kind = kind,
                Min = at,
                Max = at,
                Start = _times.Count,
                Count = 0,
            });
            _active = _spans.Count - 1;
        }

        /// <summary>
        /// Reserves a sample slot at <paramref name="at"/> and returns its index; write every
        /// channel at that index.
        /// <para>
        /// Loop time must have actually advanced since the previous sample. A frozen clock records
        /// nothing at all — there is no new loop time to describe — so a repeated instant is a
        /// caller bug and throws rather than silently growing the buffer.
        /// </para>
        /// </summary>
        public int WriteSample(LoopTime at)
        {
            var span = ActiveSpanOrThrow();
            if (span.Kind != SpanKind.Recorded)
                throw new InvalidOperationException($"{Body}: a Void span holds no samples; use Extend");

            if (span.Count > 0)
            {
                var previous = _times[_times.Count - 1];
                var advanced = span.Dir > 0 ? at.Raw > previous.Raw : at.Raw < previous.Raw;
                if (!advanced)
                    throw new ArgumentException(
                        $"{Body}: sample at {at} does not advance from {previous} (dir {span.Dir:+0;-0})",
                        nameof(at));
            }

            var index = _times.Count;
            _times.Grow(index + 1);
            _times.Set(index, at);
            foreach (var channel in _channels.Values) channel.Grow(index + 1);

            span.Count++;
            Widen(ref span, at);
            _spans[_active] = span;
            return index;
        }

        /// <summary>
        /// Grows the active void span's authority to <paramref name="at"/>. This is how destruction
        /// propagates as the cursor moves — void spans carry no samples, so they have nothing else to
        /// grow by. Recorded spans widen from their samples instead and never need this.
        /// </summary>
        public void Extend(LoopTime at)
        {
            var span = ActiveSpanOrThrow();
            if (span.Kind != SpanKind.Void)
                throw new InvalidOperationException($"{Body}: only void spans are extended; recorded spans widen from their samples");
            Widen(ref span, at);
            _spans[_active] = span;
        }

        /// <summary>
        /// Closes the active span. A recorded span that never took a sample is dropped rather than
        /// kept: it would claim authority over an instant while having no state to report there.
        /// (Void spans legitimately hold no samples.)
        /// </summary>
        public void EndSpan()
        {
            if (_active >= 0 && _spans[_active].Kind == SpanKind.Recorded && _spans[_active].Count == 0)
                _spans.RemoveAt(_active);
            _active = -1;
        }

        static void Widen(ref Span span, LoopTime at)
        {
            if (at.Raw < span.Min.Raw) span.Min = at;
            if (at.Raw > span.Max.Raw) span.Max = at;
        }

        Span ActiveSpanOrThrow() =>
            _active >= 0 ? _spans[_active] : throw new InvalidOperationException($"{Body} is not recording");

        // ------------------------------------------------------------------ reading

        /// <summary>
        /// Index of the span authoritative at <paramref name="at"/>, or -1 if none is.
        /// Spans are appended in <see cref="Span.Seq"/> order, so scanning backwards finds the
        /// highest Seq first — which is exactly the overlap rule.
        /// </summary>
        public int Resolve(LoopTime at)
        {
            for (var i = _spans.Count - 1; i >= 0; i--)
                if (_spans[i].Covers(at))
                    return i;
            return -1;
        }

        public int SeqAt(LoopTime at)
        {
            var index = Resolve(at);
            return index < 0 ? 0 : _spans[index].Seq;
        }

        /// <summary>
        /// Whether the body exists at <paramref name="at"/>. No covering span and an explicit void
        /// span both mean "no"; the difference is that a void span outranks older recordings.
        /// </summary>
        public bool Exists(LoopTime at)
        {
            var index = Resolve(at);
            return index >= 0 && _spans[index].Kind == SpanKind.Recorded;
        }

        /// <summary>
        /// Whether the cursor has walked off the end of this body's history travelling in
        /// <paramref name="dir"/> — the <b>frontier</b>, where recording has to resume (rule 4) rather than
        /// the body quietly ceasing to exist.
        /// <para>
        /// Three cases have to be told apart, and all three look like "no span covers this instant":
        /// </para>
        /// <list type="bullet">
        /// <item>past the end of a take — the frontier, so <c>true</c>: the body carries on and records;</item>
        /// <item>past the end of a <i>void</i> — it was destroyed, so <c>false</c>: it must stay dead, or
        /// rewinding and replaying past its death would resurrect it;</item>
        /// <item>outside its origin — a bullet below its muzzle, so <c>false</c>: rule 8 allows a span to
        /// grow only <i>away</i> from where the body came into being, never back across it.</item>
        /// </list>
        /// </summary>
        public bool AtFrontier(LoopTime at, int dir)
        {
            // Covered by anything, recorded or void, means this is not a frontier at all.
            if (Resolve(at) >= 0) return false;

            // The nearest span edge the way we came from. Every span counts, not just recorded ones — the
            // nearest edge is what decides whether the body was alive when we left its history.
            var found = false;
            var edge = 0;
            foreach (var span in _spans)
            {
                var candidate = dir > 0 ? span.Max.Raw : span.Min.Raw;
                if (dir > 0 ? candidate >= at.Raw : candidate <= at.Raw) continue;
                if (found && (dir > 0 ? candidate <= edge : candidate >= edge)) continue;
                edge = candidate;
                found = true;
            }

            if (!found) return false; // no history behind us: never existed here
            if (!Exists(LoopTime.FromRaw(edge))) return false; // destroyed at that edge, so it stays destroyed

            // Rule 8: the origin is a wall, and what it forbids is growing back *across* the muzzle. So the
            // test is whether this body already has history on the far side of its origin from where we are
            // heading — if it does, extending would carry it back over the muzzle.
            //
            // Not the direction the origin's span happened to be recorded in, which is too crude. A body whose
            // history is still only the origin instant itself has no far side yet and may grow either way,
            // and the turnstile depends on that: the machine's copy is emitted while the cursor runs forward,
            // then records backwards once you take it over. Both readings are legitimate — forwards it is one
            // worldline walking into the machine and out of it again — so the first growth is what picks a side.
            if (Origin.IsValid)
                foreach (var span in _spans)
                {
                    if (span.Kind != SpanKind.Recorded) continue;
                    var acrossTheMuzzle = dir > 0
                        ? span.Min.Raw < OriginAt.Raw
                        : span.Max.Raw > OriginAt.Raw;
                    if (acrossTheMuzzle) return false;
                }

            return true;
        }

        public Sampled<TState> Sample<TState>(int channelId, LoopTime at) where TState : unmanaged
        {
            var result = default(Sampled<TState>);

            var spanIndex = Resolve(at);
            if (spanIndex < 0) return result;

            var span = _spans[spanIndex];
            if (!span.HasSamples) return result;
            if (!_channels.TryGetValue(channelId, out var raw)) return result;

            var buffer = (ChannelBuffer<TState>)raw;

            var k = LowerBound(in span, at);
            if (k < 0) k = 0; // `at` precedes every sample, because Extend widened Min past them

            var ia = Physical(in span, k);
            result.Exists = true;
            result.A = buffer[ia];

            if (k + 1 < span.Count)
            {
                var ib = Physical(in span, k + 1);
                result.B = buffer[ib];
                result.T = LoopTime.InverseLerp(_times[ia], _times[ib], at);
            }
            else
            {
                // Last sample of the span: nothing to interpolate toward. Crucially we do NOT reach
                // into a neighbouring span — that boundary is a discontinuity and must snap.
                result.B = result.A;
                result.T = 0f;
            }

            return result;
        }

        /// <summary>
        /// The next instant strictly beyond <paramref name="at"/>, travelling in
        /// <paramref name="dir"/>, at which this body's applied state changes: its next sample, or a
        /// span edge where authority changes hands. False when there is nothing further that way.
        /// <para>
        /// The sim loop walks these rather than jumping straight to the frame's target instant. A
        /// divergence check only runs where a state is applied, so skipping an instant means skipping
        /// its check — and unlike a dropped frame, that silently leaves an impossible history behind.
        /// Playback bodies run no physics, only queries, so visiting them all is affordable.
        /// </para>
        /// </summary>
        public bool TryNextChange(LoopTime at, int dir, out LoopTime next)
        {
            var best = 0;
            var found = false;

            var spanIndex = Resolve(at);
            if (spanIndex >= 0)
            {
                var span = _spans[spanIndex];
                if (span.Count > 0)
                {
                    var k = LowerBound(in span, at);
                    if (dir > 0)
                    {
                        k += 1; // smallest logical index strictly after `at`
                    }
                    else if (k >= 0 && _times[Physical(in span, k)].Raw == at.Raw)
                    {
                        k -= 1; // largest strictly before it
                    }

                    if (k >= 0 && k < span.Count)
                        Offer(_times[Physical(in span, k)].Raw, at.Raw, dir, ref best, ref found);
                }
            }

            // Span edges too, since authority changing hands is a discontinuity and therefore a stop.
            // Offering an edge that turns out not to change anything merely costs a redundant visit;
            // missing one would skip a snap.
            foreach (var span in _spans)
            {
                Offer(span.Min.Raw, at.Raw, dir, ref best, ref found);
                Offer(span.Max.Raw, at.Raw, dir, ref best, ref found);
            }

            next = LoopTime.FromRaw(best);
            return found;
        }

        static void Offer(int candidate, int atRaw, int dir, ref int best, ref bool found)
        {
            if (dir > 0 ? candidate <= atRaw : candidate >= atRaw) return;
            if (found && (dir > 0 ? candidate >= best : candidate <= best)) return;
            best = candidate;
            found = true;
        }

        /// <summary>
        /// Physical index of the logical (ascending-in-time) index <paramref name="k"/>. Backward
        /// spans hold their samples in recording order, which is descending, so readers map instead
        /// of the writer reversing the data.
        /// </summary>
        static int Physical(in Span span, int k) =>
            span.Dir > 0 ? span.Start + k : span.Start + span.Count - 1 - k;

        /// <summary>Largest logical index whose time is &lt;= <paramref name="at"/>, or -1.</summary>
        int LowerBound(in Span span, LoopTime at)
        {
            int lo = 0, hi = span.Count - 1, best = -1;
            while (lo <= hi)
            {
                var mid = (lo + hi) >> 1;
                if (_times[Physical(in span, mid)].Raw <= at.Raw)
                {
                    best = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ undo

        /// <summary>
        /// Drops every span in <paramref name="layerId"/> or later. Undo is LIFO, so those spans are
        /// always a suffix of the recording order and their samples a suffix of the arrays — which
        /// makes this a truncation, with no compaction pass.
        /// </summary>
        public void TruncateFromLayer(int layerId)
        {
            var first = -1;
            for (var i = 0; i < _spans.Count; i++)
                if (_spans[i].LayerId >= layerId)
                {
                    first = i;
                    break;
                }

            if (first < 0) return;

            var sampleStart = _spans[first].Start;
            _spans.RemoveRange(first, _spans.Count - first);
            _times.Truncate(sampleStart);
            foreach (var channel in _channels.Values) channel.Truncate(sampleStart);
            _active = -1;
        }

        // ------------------------------------------------------------------ serialization

        public void WriteTo(BinaryWriter w)
        {
            w.Write(Archetype);
            w.Write(Origin.Value);
            w.Write(OriginAt.Raw);
            w.Write(_spans.Count);
            foreach (var span in _spans)
            {
                w.Write(span.Seq);
                w.Write(span.LayerId);
                w.Write(span.Min.Raw);
                w.Write(span.Max.Raw);
                w.Write(span.Dir);
                w.Write((byte)span.Kind);
                w.Write(span.Start);
                w.Write(span.Count);
                w.Write(span.CausedBy.Value);
                w.Write(span.CausedAt.Raw);
            }

            _times.WriteTo(w);

            w.Write(_channels.Count);
            foreach (var pair in _channels)
            {
                w.Write(pair.Key);
                pair.Value.WriteTo(w);
            }
        }

        public void ReadFrom(BinaryReader r, ChannelRegistry registry)
        {
            _spans.Clear();
            _channels.Clear();
            _active = -1;

            Archetype = r.ReadInt32();
            Origin = new SimId(r.ReadInt32());
            OriginAt = LoopTime.FromRaw(r.ReadInt32());
            var spanCount = r.ReadInt32();
            for (var i = 0; i < spanCount; i++)
                _spans.Add(new Span
                {
                    Seq = r.ReadInt32(),
                    LayerId = r.ReadInt32(),
                    Min = LoopTime.FromRaw(r.ReadInt32()),
                    Max = LoopTime.FromRaw(r.ReadInt32()),
                    Dir = r.ReadSByte(),
                    Kind = (SpanKind)r.ReadByte(),
                    Start = r.ReadInt32(),
                    Count = r.ReadInt32(),
                    CausedBy = new SimId(r.ReadInt32()),
                    CausedAt = LoopTime.FromRaw(r.ReadInt32()),
                });

            _times.ReadFrom(r);

            var channelCount = r.ReadInt32();
            for (var i = 0; i < channelCount; i++)
            {
                var channelId = r.ReadInt32();
                var buffer = registry.Create(channelId);
                buffer.ReadFrom(r);
                _channels.Add(channelId, buffer);
            }
        }
    }
}
