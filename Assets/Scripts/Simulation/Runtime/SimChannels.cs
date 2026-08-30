namespace Chronomancers.Sim.Runtime
{
    /// <summary>
    /// Every channel id in the game, in one place.
    /// <para>
    /// Ids are baked into saved histories, so they are assigned once and never reused or renumbered —
    /// the same contract as <see cref="ArchetypeRegistry"/> entries. Registration lives next to the
    /// constants deliberately: a channel that exists but was never registered loads as a hard failure
    /// (<see cref="ChannelRegistry.Create"/> throws), and keeping the two lists adjacent is what stops
    /// them drifting apart.
    /// </para>
    /// </summary>
    public static class SimChannels
    {
        public const int Pose = 1;
        public const int Body2D = 2;
        public const int Health = 3;
        public const int Door = 4;

        /// <summary>
        /// Retired. Projectiles carried an <c>Absorbed</c> flag until the contact set on
        /// <see cref="SimRigidbody2D"/> made it unnecessary. Never reuse the id: an old save still names
        /// it, and loading one must fail loudly rather than deserialize into the wrong struct.
        /// </summary>
        public const int RetiredProjectile = 5;

        public const int Character = 6;
        public const int Gun = 7;
        public const int TimeMachine = 8;

        public static void RegisterAll(ChannelRegistry registry)
        {
            registry.Register<PoseState>(Pose);
            registry.Register<Body2DState>(Body2D);
            registry.Register<HealthState>(Health);
            registry.Register<DoorState>(Door);
            registry.Register<CharacterState>(Character);
            registry.Register<GunState>(Gun);
            registry.Register<MachineState>(TimeMachine);
        }
    }
}
