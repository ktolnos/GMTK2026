namespace Chronomancers.Sim
{
    /// <summary>
    /// Result of sampling one channel at one instant.
    /// <para>
    /// Continuous channels (position, rotation, velocity) lerp <see cref="A"/> to <see cref="B"/>
    /// by <see cref="T"/>. Discrete channels (hp, ammo, held item, animation state) must read
    /// <see cref="Snap"/> and never interpolate. Only the component knows which of its fields are
    /// which, so the container hands back both endpoints instead of a blended value.
    /// </para>
    /// </summary>
    public struct Sampled<TState> where TState : unmanaged
    {
        /// <summary>False when no span is authoritative here, or the body does not exist here.</summary>
        public bool Exists;

        public TState A, B;

        /// <summary>0 at A, 1 at B. Always 0 when A is the last sample in its span.</summary>
        public float T;

        public TState Snap => A;
    }
}