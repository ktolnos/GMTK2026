using UnityEngine;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>
    /// Whether this projectile was absorbed, and by what. Ordinary recorded state, which is the whole
    /// trick — it dies with its span, so it needs no once-only delivery and no event log.
    /// </summary>
    public struct ProjectileState
    {
        public byte Absorbed;
        public int AbsorbedBy;
    }

    /// <summary>
    /// A bullet. Short-lived, spawned at a muzzle, and the awkward case for nearly every rule.
    /// <para>
    /// It refuses contact claims (see <see cref="AcceptsContactClaim"/>): re-recording a bullet in the
    /// cursor's direction would overwrite the end of its span that holds the muzzle, and rule 8 forbids a
    /// bullet that appears to have been emitted by the wall it hit.
    /// </para>
    /// <para>
    /// So an inverted character running into a recorded bullet does not destroy it. Voiding the low side
    /// would erase the muzzle; writing the high side is forbidden outright (rule 3). Instead the hit is
    /// recorded as a single-sample <c>Absorbed</c> flag, and the tail beyond it is retired later, on a
    /// forward pass — when the cursor is finally moving in a direction allowed to write it.
    /// </para>
    /// </summary>
    public sealed class SimBullet : SimulatedComponent<ProjectileState>
    {
        [SerializeField] int damage = 1;

        byte _absorbed;
        int _absorbedBy;

        public override int ChannelId => SimChannels.Projectile;

        /// <inheritdoc/>
        internal override bool AcceptsContactClaim => false;

        protected override ProjectileState Capture() =>
            new ProjectileState { Absorbed = _absorbed, AbsorbedBy = _absorbedBy };

        protected override void Apply(in Sampled<ProjectileState> sampled)
        {
            _absorbed = sampled.A.Absorbed;
            _absorbedBy = sampled.A.AbsorbedBy;
        }

        protected override void Validate(LoopTime at, in Sampled<ProjectileState> sampled)
        {
            if (sampled.A.Absorbed == 0) return;

            // Only forwards. Backwards, everything past this instant is behind the cursor and rule 3
            // forbids touching it; the timeline is simply left transiently stale until a forward pass
            // reaches here. The repair is idempotent — the guard is whether the bullet still exists past
            // the hit — so nothing has to remember whether it has run.
            if (Dir <= 0) return;

            Runner.RequestKill(Body.Id, new SimId(sampled.A.AbsorbedBy), "absorbed on an earlier pass");
        }

        internal override void OnClaimed(int dir)
        {
            _absorbed = 0;
            _absorbedBy = 0;
        }

        protected override void ResetState()
        {
            _absorbed = 0;
            _absorbedBy = 0;
        }

        void OnCollisionEnter2D(Collision2D collision) => Hit(collision.collider);

        void OnTriggerEnter2D(Collider2D other) => Hit(other);

        void Hit(Collider2D other)
        {
            if (Runner == null || !other) return;

            var theirs = other.GetComponentInParent<SimBody>();
            if (theirs == Body) return;

            if (IsRecording)
            {
                // Our own flight: an ordinary hit. We exist at the instant we are destroyed, so the void
                // begins one unit beyond it — otherwise the damage we just dealt would lose its cause.
                if (theirs != null)
                {
                    var health = theirs.GetComponent<SimHealth>();
                    if (health != null) health.TakeDamage(damage, Body.Id);
                }

                Runner.RequestKill(Body.Id, theirs != null ? theirs.Id : SimId.None, "struck something");
                return;
            }

            // We are being played back and something live ran into us. That something is travelling
            // against our recorded flight, so this is an absorption rather than a hit.
            if (theirs == null || !theirs.IsRecording) return;

            var victim = theirs.GetComponent<SimHealth>();
            if (victim != null) victim.TakeDamage(damage, Body.Id);

            _absorbed = 1;
            _absorbedBy = theirs.Id.Value;
            Runner.RequestAbsorb(Body.Id);
        }
    }
}
