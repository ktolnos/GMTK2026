using UnityEngine;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>
    /// Aim, in degrees. Continuous, so it interpolates — but on the shortest arc, or a character turning
    /// past 180 would be replayed spinning the long way round.
    /// </summary>
    public struct CharacterState
    {
        public float Aim;
    }

    /// <summary>
    /// A top-down character: two-axis movement, and an aim that is independent of it.
    /// <para>
    /// Aim is a recorded channel rather than something derived from movement, because it decides where
    /// bullets go and is therefore gameplay-relevant (rule 12). Walking backwards while covering a doorway
    /// has to replay exactly as performed, so the direction the gun pointed cannot be reconstructed from
    /// the path.
    /// </para>
    /// <para>
    /// With no intent the character is <b>inert, not dead</b> (rule 11). It keeps its momentum, gets pushed,
    /// still dies, and is still a switch target. Almost every body in the first take is one of these, doing
    /// nothing.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class SimCharacter : SimulatedComponent<CharacterState>
    {
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] float interactRadius = 1.5f;

        [SerializeField]
        [Tooltip("Rotate the sprite to match the aim. Off for anything whose art is not directional.")]
        bool faceAim = true;

        Rigidbody2D _rigidbody;
        float _aim;
        bool _interactHeld;

        public override int ChannelId => SimChannels.Character;

        /// <summary>Aim in degrees, counter-clockwise from +X. What the gun fires along.</summary>
        public float Aim => _aim;

        public Vector2 AimDirection
        {
            get
            {
                var radians = _aim * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            }
        }

        void Awake() => _rigidbody = GetComponent<Rigidbody2D>();

        protected override void ResetState()
        {
            _aim = 0f;
            _interactHeld = false;
        }

        protected override CharacterState Capture() => new CharacterState { Aim = _aim };

        protected override void Apply(in Sampled<CharacterState> sampled)
        {
            _aim = Mathf.LerpAngle(sampled.A.Aim, sampled.B.Aim, sampled.T);
            if (faceAim) _rigidbody.rotation = _aim;
        }

        internal override void Simulate(LoopTime at, int movedRaw)
        {
            var intent = Runner.IntentFor(Body);

            // Normalised so diagonals are not faster, but only when it would otherwise exceed 1 — an
            // analogue stick at half deflection should stay at half speed.
            var move = intent.Move;
            if (move.sqrMagnitude > 1f) move = move.normalized;
            _rigidbody.linearVelocity = move * moveSpeed;

            if (intent.HasAim)
            {
                var toTarget = intent.Aim - _rigidbody.position;
                if (toTarget.sqrMagnitude > 1e-6f)
                    _aim = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            }
            else if (move.sqrMagnitude > 1e-6f)
            {
                // No aim source — an inert body, or a pad with no stick. Face where it is walking.
                _aim = Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg;
            }

            if (faceAim) _rigidbody.rotation = _aim;

            // Edge-detected here rather than in the input source, so a held key does not toggle a door
            // every step. The edge is in loop time as much as in real time: the key re-arms only on
            // release, so rewinding and replaying does not re-fire it.
            if (intent.Interact && !_interactHeld) Interact();
            _interactHeld = intent.Interact;
        }

        /// <summary>
        /// Finds interactables by distance over the live bodies, deliberately <i>not</i> with an overlap query.
        /// <para>
        /// A physics query cannot see two of the things it needs to. An open door has its blocker disabled, so
        /// it drops out of queries entirely and could never be shut again — a one-way door. And a machine whose
        /// collider is a trigger is invisible to queries whenever the project has <c>queriesHitTriggers</c>
        /// off, which is a setting nothing here controls.
        /// </para>
        /// <para>
        /// Interaction range has nothing to do with collision anyway, so taking it from geometry that exists for
        /// collision was the mistake. The timeline already knows every live body.
        /// </para>
        /// </summary>
        void Interact()
        {
            if (Runner == null) return;

            var here = (Vector2)transform.position;

            foreach (var pair in Runner.Live)
            {
                var theirs = pair.Value;
                if (theirs == Body) continue;

                if (((Vector2)theirs.transform.position - here).sqrMagnitude > interactRadius * interactRadius)
                    continue;

                foreach (var component in theirs.Components)
                    if (component is ISimInteractable interactable)
                        interactable.Interact(Body);
            }
        }
    }
}
