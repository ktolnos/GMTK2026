using UnityEngine;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>Position and rotation. Entirely continuous, so all of it interpolates.</summary>
    public struct PoseState
    {
        public float X;
        public float Y;
        public float Rot;
    }

    /// <summary>
    /// Records where a body is. Nearly every body wants one.
    /// <para>
    /// Playback drives the rigidbody with <see cref="Rigidbody2D.MovePosition"/>, never the
    /// <see cref="Transform"/>. Writing a Transform teleports the collider: it sweeps nothing, so it
    /// generates no contacts on the way and can pass straight through a thin body, and it leaves the
    /// physics engine needing a <see cref="Physics2D.SyncTransforms"/> to catch up. A kinematic body
    /// moved properly sweeps between poses, which is what lets a recorded platform carry a live
    /// character and a recorded door shove one aside.
    /// </para>
    /// <para>
    /// The move lands during the next <c>Physics2D.Simulate</c> rather than immediately, so anything
    /// checking where a body is must read its <i>recorded</i> pose from the timeline rather than its live
    /// Transform — which is what the divergence checks already do. And when no step is coming at all,
    /// the pose is written straight to <c>Rigidbody2D.position</c> instead, or a rewind would freeze
    /// everything (see <see cref="SimRunner.PhysicsWillStep"/>).
    /// </para>
    /// <para>
    /// Capture reads the rigidbody too, because it runs directly after <c>Physics2D.Simulate</c> where
    /// the body is the authority and the Transform is its output.
    /// </para>
    /// </summary>
    public sealed class SimTransform : SimulatedComponent<PoseState>
    {
        Rigidbody2D _rigidbody;

        public override int ChannelId => SimChannels.Pose;

        void Awake() => _rigidbody = GetComponent<Rigidbody2D>();

        protected override PoseState Capture()
        {
            var position = _rigidbody != null ? _rigidbody.position : (Vector2)transform.position;
            var rotation = _rigidbody != null ? _rigidbody.rotation : transform.eulerAngles.z;
            return new PoseState { X = position.x, Y = position.y, Rot = rotation };
        }

        protected override void Apply(in Sampled<PoseState> sampled)
        {
            var position = new Vector2(
                Mathf.Lerp(sampled.A.X, sampled.B.X, sampled.T),
                Mathf.Lerp(sampled.A.Y, sampled.B.Y, sampled.T));

            // Shortest-arc, so a body spinning past 180 degrees does not unwind the long way round.
            var rotation = Mathf.LerpAngle(sampled.A.Rot, sampled.B.Rot, sampled.T);

            if (_rigidbody != null)
            {
                // A queued move is only carried out by a physics step, and rule 4 skips the step entirely
                // when nothing is recording. Rewinding through known territory would otherwise leave every
                // body standing exactly where it was.
                if (Runner != null && Runner.PhysicsWillStep)
                {
                    _rigidbody.MovePosition(position);
                    _rigidbody.MoveRotation(rotation);
                }
                else
                {
                    _rigidbody.position = position;
                    _rigidbody.rotation = rotation;
                }
                return;
            }

            transform.SetPositionAndRotation(
                new Vector3(position.x, position.y, transform.position.z),
                Quaternion.Euler(0f, 0f, rotation));
        }
    }
}
