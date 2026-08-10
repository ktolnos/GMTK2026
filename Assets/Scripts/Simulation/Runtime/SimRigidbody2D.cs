using UnityEngine;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>Velocities, so that momentum survives a claim.</summary>
    public struct Body2DState
    {
        public float VX;
        public float VY;
        public float VAngular;
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
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class SimRigidbody2D : SimulatedComponent<Body2DState>
    {
        Rigidbody2D _rigidbody;
        RigidbodyType2D _authoredType;

        public override int ChannelId => SimChannels.Body2D;

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _authoredType = _rigidbody.bodyType;
        }

        protected override Body2DState Capture() => new Body2DState
        {
            VX = _rigidbody.linearVelocity.x,
            VY = _rigidbody.linearVelocity.y,
            VAngular = _rigidbody.angularVelocity,
        };

        protected override void Apply(in Sampled<Body2DState> sampled)
        {
            if (_rigidbody.bodyType == RigidbodyType2D.Dynamic)
                _rigidbody.bodyType = RigidbodyType2D.Kinematic;

            // Snapped, not blended: velocity is a derivative, and the seam between two takes is a real
            // discontinuity. Blending across it would invent a motion neither take contained.
            _rigidbody.linearVelocity = new Vector2(sampled.A.VX, sampled.A.VY);
            _rigidbody.angularVelocity = sampled.A.VAngular;
        }

        internal override void OnClaimed(int dir)
        {
            // Rule 11: inert is not dead. A crate claimed mid-slide keeps sliding at the speed history last
            // recorded, so the claim is continuous in velocity as well as in position (rule 5).
            //
            // With no sample to inherit — a body spawned this instant — whatever it was just handed stands
            // instead. That is how a bullet keeps the muzzle velocity the gun gave it.
            var linear = Last.Exists ? new Vector2(Last.A.VX, Last.A.VY) : _rigidbody.linearVelocity;
            var angular = Last.Exists ? Last.A.VAngular : _rigidbody.angularVelocity;

            // Whatever the body was authored as — a static wall stays static when claimed.
            _rigidbody.bodyType = _authoredType;

            // Written after the body type change, not before: changing type can reset velocities, so an
            // assignment made first would silently vanish.
            _rigidbody.linearVelocity = linear;
            _rigidbody.angularVelocity = angular;
        }

        internal override void OnReleased()
        {
            if (_rigidbody.bodyType == RigidbodyType2D.Dynamic)
                _rigidbody.bodyType = RigidbodyType2D.Kinematic;
        }
    }
}
