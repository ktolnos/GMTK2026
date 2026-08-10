using UnityEngine;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>Discrete. Never interpolated — half a hit point is not a state anything was ever in.</summary>
    public struct HealthState
    {
        public int Hp;
    }

    /// <summary>
    /// Hit points, and the check that they only ever drop for a reason.
    /// <para>
    /// This is the granular counterpart to the door: the door's transform check notices a recorded
    /// position that has become impossible, and this notices a recorded <i>HP drop</i> that has become
    /// impossible. Same trigger, same repair, different channel — which is the point of rule 11 having
    /// one mechanism rather than a list of special cases.
    /// </para>
    /// </summary>
    public sealed class SimHealth : SimulatedComponent<HealthState>
    {
        [SerializeField] int maxHp = 3;

        [SerializeField]
        [Tooltip("How far to look for something that could have caused a recorded HP drop.")]
        float damageSearchRadius = 0.75f;

        int _hp;

        public override int ChannelId => SimChannels.Health;

        public int Hp => _hp;
        public bool Alive => _hp > 0;

        void Awake() => _hp = maxHp;

        protected override void ResetState() => _hp = maxHp;

        protected override HealthState Capture() => new HealthState { Hp = _hp };

        protected override void Apply(in Sampled<HealthState> sampled) => _hp = sampled.A.Hp;

        protected override void Validate(LoopTime at, in Sampled<HealthState> sampled)
        {
            // A and B are always ordered ascending in loop time regardless of which direction the span
            // was recorded in, so "dropped" means the same thing either way.
            if (sampled.B.Hp >= sampled.A.Hp) return;

            if (Runner == null) return;
            if (Runner.AnyDamageSourceNear(transform.position, damageSearchRadius, Body)) return;

            Runner.RequestClaim(Body.Id,
                $"recorded HP drop {sampled.A.Hp}->{sampled.B.Hp} with no damage dealer to account for it");
        }

        /// <summary>
        /// Applies damage to a body that is being recorded. Playback bodies are pointedly excluded: their
        /// HP comes from history, and letting a live event write it would be an accumulated state — the
        /// thing rule 10 exists to prevent.
        /// </summary>
        public void TakeDamage(int amount, SimId from)
        {
            if (!IsRecording || amount <= 0) return;

            _hp = Mathf.Max(0, _hp - amount);
            if (_hp == 0) Runner.RequestKill(Body.Id, from, "hit points reached zero");
        }
    }
}
