namespace Chronomancers.Sim
{
    /// <summary>
    /// Whether a body is in the world at an instant. Recorded per sample, never interpolated.
    /// <para>
    /// Matter is conserved (rule 7): nothing is created and nothing is destroyed, so a body that is not
    /// in the world is <see cref="Latent"/> rather than absent — still in the gun, absorbed, evaporated.
    /// One state covers all of those; the difference between them is narrative, not mechanical.
    /// </para>
    /// <para>
    /// <see cref="Latent"/> is zero deliberately. A body with no span covering an instant reads as latent
    /// by default, which is what makes conservation free: there is no roster to pre-populate, and the
    /// turnstile can mint copies without limit.
    /// </para>
    /// </summary>
    public enum Form : byte
    {
        Latent = 0,
        Manifest = 1,
    }

    /// <summary>
    /// One continuous recording pass over a single body, in a single time direction.
    /// <para>
    /// Spans are the unit of authority: wherever two overlap, the higher <see cref="Seq"/> wins. That one
    /// rule covers overwriting a previous take, re-recording the same range twice within one take (the
    /// cursor doubling back), and propagating a death back over loop time the body used to occupy — a
    /// span whose samples read <see cref="Form.Latent"/> outranks an older one that says otherwise.
    /// </para>
    /// <para>
    /// Interpolation never crosses a span boundary, which makes boundaries the only true discontinuities
    /// in the game. A discontinuity is impossible read in either direction, so a state change must happen
    /// <i>inside</i> a span and never on its edge (rule 6).
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

        /// <summary>
        /// Slice into the body's sample arrays, in <i>recording</i> order — so descending in time
        /// when <see cref="Dir"/> is negative. Readers map a logical ascending index onto this
        /// rather than the writer reversing the data.
        /// </summary>
        public int Start, Count;

        public bool Covers(LoopTime at) => at.Raw >= Min.Raw && at.Raw <= Max.Raw;

        public bool HasSamples => Count > 0;

        public override string ToString() =>
            $"span#{Seq} L{LayerId} [{Min}..{Max}] dir{Dir:+0;-0} x{Count}";
    }
}
