using UnityEngine;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>
    /// Cooldown in loop-time units, not seconds. A cooldown counted in real time would run at a
    /// different speed in bullet time and would survive a rewind untouched, which is precisely the
    /// desync rule 12 exists to catch.
    /// </summary>
    public struct GunState
    {
        public int CooldownRaw;
    }

    /// <summary>
    /// Fires bullets. The only thing here that is not obvious is that it never instantiates anything: it
    /// asks the runner for a spawn, and the runner materialises the body, gives it a deterministic id and
    /// an origin, and opens its span at the muzzle.
    /// </summary>
    public sealed class SimGun : SimulatedComponent<GunState>
    {
        [SerializeField]
        [Tooltip("ArchetypeRegistry id of the bullet prefab.")]
        int bulletArchetype = 1;

        [SerializeField] float muzzleOffset = 0.6f;
        [SerializeField] float bulletSpeed = 14f;
        [SerializeField] float cooldownSeconds = 0.2f;

        SimCharacter _character;
        int _cooldown;

        public override int ChannelId => SimChannels.Gun;

        void Awake() => _character = GetComponentInParent<SimCharacter>();

        protected override void ResetState() => _cooldown = 0;

        protected override GunState Capture() => new GunState { CooldownRaw = _cooldown };

        protected override void Apply(in Sampled<GunState> sampled) => _cooldown = sampled.A.CooldownRaw;

        internal override void Simulate(LoopTime at, int movedRaw)
        {
            _cooldown = Mathf.Max(0, _cooldown - movedRaw);

            var intent = Runner.IntentFor(Body);
            if (!intent.Fire || _cooldown > 0) return;

            var aim = _character != null ? _character.Aim : 0f;
            var direction = _character != null ? _character.AimDirection : Vector2.right;
            var muzzle = (Vector2)transform.position + direction * muzzleOffset;

            // The bullet is given the cursor's direction by the runner, not the shooter's aim. A character
            // being recorded backwards emits a bullet that records backwards too, which read forwards is a
            // bullet flying *into* the muzzle — the inverted-causality signature, not a bug.
            Runner.RequestSpawn(Body.Id, bulletArchetype, muzzle, aim, direction * bulletSpeed);

            _cooldown = LoopTime.FromSeconds(cooldownSeconds).Raw;
        }
    }
}
