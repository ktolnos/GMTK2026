using UnityEngine;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>
    /// Velocities, so that momentum survives a claim — and the <b>contact set</b>: who pushed this body
    /// hard enough to matter, at this sample.
    /// </summary>
    public struct Body2DState
    {
        public float VX;
        public float VY;
        public float VAngular;

        /// <summary>
        /// Ids of the bodies whose contact accounted for this body's motion. Zero means an empty slot.
        /// Four is a cap, not a law: a floor plus a pusher plus slack.
        /// </summary>
        public int Contact0;
        public int Contact1;
        public int Contact2;
        public int Contact3;
    }

    /// <summary>
    /// Records a rigidbody's motion, and owns the switch between being driven by physics and being
    /// driven by history.
    /// <para>
    /// A playback body becomes <b>kinematic, not unsimulated</b>. It has to stay in the physics world:
    /// a recorded crate must still block a recording character, and a recorded floor must still hold one
    /// up. Taking it out of the simulation entirely would let live bodies pass straight through
    /// everything that is merely being replayed.
    /// </para>
    /// <para>
    /// Velocity is applied to the kinematic body as well as position. Position alone is a teleport, and
    /// contacts resolved against a teleporting collider get no relative velocity to work with — a
    /// recorded platform would not carry anything standing on it.
    /// </para>
    /// <para>
    /// <b>The contact set (rule 8).</b> A body records what pushed it, and on playback checks that those
    /// things are still there. That is what catches an uncaused acceleration: erase the guard who shoved
    /// a lamp and the lamp's recorded contact partner is gone, so the lamp diverges instead of toppling
    /// over on its own. It deliberately is not force accounting — deciding whether a recorded velocity
    /// change is "explained by gravity and friction" means re-deriving dynamics outside the engine, which
    /// is most of a second physics engine. Asking <i>who touched me</i> needs no dynamics at all: gravity
    /// is universal and identical across takes, and friction arrives through a contact already in the set.
    /// </para>
    /// <para>
    /// Note which side each half runs on. <b>Writing</b> uses the engine's own impulses, because a
    /// recording body really is physics-driven and <c>Collision2D.impulse</c> is exact there.
    /// <b>Checking</b> uses distance between recorded poses, because a playback body is driven by history
    /// and its contacts are not reliably generated — and because a distance test is independent of which
    /// step a contact happened to be detected on, which a set of engine events is not.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class SimRigidbody2D : SimulatedComponent<Body2DState>
    {
        [Header("Contact set")]
        [SerializeField]
        [Tooltip("A contact is recorded once it has changed this body's velocity by this much, in units " +
                 "per second. Below it, nothing is recorded and nothing diverges — brushing past a " +
                 "recorded crate must not claim it.")]
        float significantDeltaV = 0.05f;

        [SerializeField]
        [Tooltip("How far a recorded contact partner may have moved before this body's recording no " +
                 "longer stands up. Roughly the size of a body plus a margin.")]
        float contactRecallRadius = 1.25f;

        Rigidbody2D _rigidbody;
        RigidbodyType2D _authoredType;

        // Accumulated across the physics step, drained by Capture. Parallel arrays rather than a dict:
        // this is on the hot path and never holds more than a handful of entries.
        readonly int[] _partners = new int[ContactSlots];
        readonly float[] _pushed = new float[ContactSlots];

        const int ContactSlots = 4;

        public override int ChannelId => SimChannels.Body2D;

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _authoredType = _rigidbody.bodyType;
        }

        protected override Body2DState Capture()
        {
            var state = new Body2DState
            {
                VX = _rigidbody.linearVelocity.x,
                VY = _rigidbody.linearVelocity.y,
                VAngular = _rigidbody.angularVelocity,
            };

            // Only the partners that actually moved us. Everything else was a brush.
            var kept = 0;
            for (var i = 0; i < ContactSlots; i++)
            {
                if (_partners[i] != 0 && _pushed[i] >= significantDeltaV)
                {
                    switch (kept++)
                    {
                        case 0: state.Contact0 = _partners[i]; break;
                        case 1: state.Contact1 = _partners[i]; break;
                        case 2: state.Contact2 = _partners[i]; break;
                        case 3: state.Contact3 = _partners[i]; break;
                    }
                }

                _partners[i] = 0;
                _pushed[i] = 0f;
            }

            return state;
        }

        protected override void Apply(in Sampled<Body2DState> sampled)
        {
            if (_rigidbody.bodyType == RigidbodyType2D.Dynamic)
                _rigidbody.bodyType = RigidbodyType2D.Kinematic;

            // Snapped, not blended: velocity is a derivative, and the seam between two takes is a real
            // discontinuity. Blending across it would invent a motion neither take contained.
            _rigidbody.linearVelocity = new Vector2(sampled.A.VX, sampled.A.VY);
            _rigidbody.angularVelocity = sampled.A.VAngular;
        }

        /// <summary>
        /// Every body this recording says pushed us has to still be here, near enough to have done it.
        /// One that is gone or somewhere else means the recorded motion is not the motion this body would
        /// now have, so it diverges and re-simulates from this instant (rule 8).
        /// </summary>
        protected override void Validate(LoopTime at, in Sampled<Body2DState> sampled)
        {
            // Only playback needs checking. A recording body is being pushed by the live world right now,
            // so there is nothing recorded to disagree with — and the base hands it a synthetic sample.
            if (IsRecording || Runner == null || Body == null) return;

            if (Missing(sampled.A.Contact0, at)) return;
            if (Missing(sampled.A.Contact1, at)) return;
            if (Missing(sampled.A.Contact2, at)) return;
            if (Missing(sampled.A.Contact3, at)) return;
        }

        /// <summary>
        /// Whether <paramref name="partnerId"/> can no longer account for having pushed us at
        /// <paramref name="at"/>. Requests the claim itself and reports true so the caller can stop —
        /// there is nothing more to learn once the body is already diverging.
        /// </summary>
        bool Missing(int partnerId, LoopTime at)
        {
            if (partnerId == 0) return false;

            var partner = new SimId(partnerId);
            var timeline = Runner.Timeline;

            if (!timeline.Exists(partner, at))
            {
                Runner.RequestClaim(Body.Id, $"recorded contact {partner} is no longer in the world");
                return true;
            }

            // Recorded poses on both sides, never live transforms: a playback body is moved by
            // MovePosition and so is not where the engine thinks it is until the next step.
            if (!timeline.TryGetBody(partner, out var partnerTimeline)) return false;
            if (!timeline.TryGetBody(Body.Id, out var ownTimeline)) return false;

            var theirs = partnerTimeline.Sample<PoseState>(SimChannels.Pose, at);
            var ours = ownTimeline.Sample<PoseState>(SimChannels.Pose, at);
            if (!theirs.Exists || !ours.Exists) return false;

            var apart = new Vector2(theirs.A.X - ours.A.X, theirs.A.Y - ours.A.Y);
            if (apart.sqrMagnitude <= contactRecallRadius * contactRecallRadius) return false;

            Runner.RequestClaim(Body.Id,
                $"recorded contact {partner} is now {apart.magnitude:0.00} away and cannot have pushed us");
            return true;
        }

        internal override void OnClaimed(int dir)
        {
            // Rule 10: inert is not dead. A crate claimed mid-slide keeps sliding at the speed history last
            // recorded, so the claim is continuous in velocity as well as in position (rule 5).
            //
            // With no sample to inherit — a body released this instant — whatever it was just handed stands
            // instead. That is how a bullet keeps the muzzle velocity the gun gave it.
            var linear = Last.Exists ? new Vector2(Last.A.VX, Last.A.VY) : _rigidbody.linearVelocity;
            var angular = Last.Exists ? Last.A.VAngular : _rigidbody.angularVelocity;

            // Whatever the body was authored as — a static wall stays static when claimed.
            _rigidbody.bodyType = _authoredType;

            // Written after the body type change, not before: changing type can reset velocities, so an
            // assignment made first would silently vanish.
            _rigidbody.linearVelocity = linear;
            _rigidbody.angularVelocity = angular;

            ResetState();
        }

        internal override void OnReleased()
        {
            if (_rigidbody.bodyType == RigidbodyType2D.Dynamic)
                _rigidbody.bodyType = RigidbodyType2D.Kinematic;

            ResetState();
        }

        protected override void ResetState()
        {
            for (var i = 0; i < ContactSlots; i++)
            {
                _partners[i] = 0;
                _pushed[i] = 0f;
            }
        }

        // ------------------------------------------------------------------ writing the contact set

        void OnCollisionEnter2D(Collision2D collision) => Pushed(collision);

        void OnCollisionStay2D(Collision2D collision) => Pushed(collision);

        /// <summary>
        /// Accumulates how much this contact has changed our velocity. Accumulated rather than tested per
        /// step deliberately: leaning on something applies a small impulse every step and would never
        /// cross the threshold on its own, but it certainly ought to.
        /// </summary>
        void Pushed(Collision2D collision)
        {
            if (!IsRecording || _rigidbody == null) return;

            var theirs = collision.collider != null
                ? collision.collider.GetComponentInParent<SimBody>()
                : null;
            if (theirs == null || theirs == Body) return;

            var mass = _rigidbody.mass > 0f ? _rigidbody.mass : 1f;
            var deltaV = ImpulseOf(collision) / mass;
            if (deltaV <= 0f) return;

            var id = theirs.Id.Value;
            for (var i = 0; i < ContactSlots; i++)
            {
                if (_partners[i] != id && _partners[i] != 0) continue;
                _partners[i] = id;
                _pushed[i] += deltaV;
                return;
            }
            // Slots full: the four things already pushing us are enough to explain our motion.
        }

        /// <summary>
        /// Total impulse this collision applied to us, summed over its contact points.
        /// <para>
        /// 2D has no <c>Collision2D.impulse</c> — that is the 3D API. Each
        /// <see cref="ContactPoint2D"/> carries its own <see cref="ContactPoint2D.normalImpulse"/> and
        /// <see cref="ContactPoint2D.tangentImpulse"/>, which are perpendicular, so the per-point
        /// magnitude is their hypotenuse. Friction counts as much as the normal force here: being
        /// dragged along by a moving platform is exactly as much "something pushed me" as being shoved.
        /// </para>
        /// <para>
        /// Read through <c>contactCount</c>/<c>GetContact</c> rather than <c>GetContacts</c> so nothing
        /// allocates — this runs inside <c>Physics2D.Simulate</c>, for every contact, every step.
        /// </para>
        /// </summary>
        static float ImpulseOf(Collision2D collision)
        {
            var total = 0f;
            for (var i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                total += Mathf.Sqrt(
                    contact.normalImpulse * contact.normalImpulse +
                    contact.tangentImpulse * contact.tangentImpulse);
            }
            return total;
        }
    }
}
