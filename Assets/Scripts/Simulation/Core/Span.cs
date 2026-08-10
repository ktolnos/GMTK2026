namespace Chronomancers.Sim
{
    public enum SpanKind : byte
    {
        /// <summary>The body existed and was sampled over this range.</summary>
        Recorded = 0,

        /// <summary>
        /// The body explicitly did NOT exist over this range. Different from "no span covers this
        /// instant": a void span carries a <see cref="Span.Seq"/> and therefore overrides older
        /// spans, which is how non-existence propagates back over a range that used to hold a
        /// recording.
        /// </summary>
        Void = 1,
    }

    /// <summary>
    /// One continuous recording pass over a single body, in a single time direction.
    /// <para>
    /// Spans are the unit of authority: wherever two overlap, the higher <see cref="Seq"/> wins.
    /// That one rule covers overwriting a previous take, re-recording the same range twice within
    /// one take (the cursor doubling back), and propagating non-existence.
    /// </para>
    /// <para>
    /// Interpolation never crosses a span boundary. The seam between a forward pass and a backward
    /// pass over the same range is a real discontinuity — full HP on one side, dead on the other —
    /// and must snap.
    /// </para>
    /// </summary>
    public struct Span
    {
        /// <summary>Global recording order. Higher wins on overlap.</summary>
        public int Seq;

        /// <summary>Undo grouping: one layer per player action, including its divergence cascade.</summary>
        public int LayerId;

        /// <summary>Inclusive range of loop time this span is authoritative over.</summary>
        public LoopTime Min, Max;

        /// <summary>+1 or -1: the direction the cursor moved while this span was recorded.</summary>
        public sbyte Dir;

        public SpanKind Kind;

        /// <summary>
        /// Slice into the body's sample arrays, in <i>recording</i> order — so descending in time
        /// when <see cref="Dir"/> is negative. Readers map a logical ascending index onto this
        /// rather than the writer reversing the data.
        /// </summary>
        public int Start, Count;

        /// <summary>
        /// Void spans only: what destroyed the body, and the instant it did.
        /// <para>
        /// A destruction is legitimate only while its cause still exists at that instant. Otherwise it
        /// could not have happened, and the body must be revived and re-simulated. A void has exactly
        /// one cause, which is why this lives on the span instead of needing a separate event.
        /// </para>
        /// <para>
        /// <see cref="SimId.None"/> means uncaused — fell out of the world, expired, scripted.
        /// </para>
        /// </summary>
        public SimId CausedBy;

        public LoopTime CausedAt;

        public bool Covers(LoopTime at) => at.Raw >= Min.Raw && at.Raw <= Max.Raw;

        public bool HasSamples => Kind == SpanKind.Recorded && Count > 0;

        public override string ToString() =>
            $"span#{Seq} L{LayerId} {Kind} [{Min}..{Max}] dir{Dir:+0;-0} x{Count}";
    }
}