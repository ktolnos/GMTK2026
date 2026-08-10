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
    /// Playback writes the <see cref="Transform"/>, never <c>Rigidbody2D.position</c>. The runner calls
    /// <see cref="Physics2D.SyncTransforms"/> immediately afterwards so its overlap queries see the new
    /// poses — and that call pushes <i>Transform into the physics body</i>, not the reverse. Writing the
    /// rigidbody here would therefore be undone a moment later by the stale Transform, which is exactly
    /// what made every played-back body sit still.
    /// </para>
    /// <para>
    /// Capture reads the rigidbody instead, because it runs directly after <c>Physics2D.Simulate</c> where
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

            transform.SetPositionAndRotation(
                new Vector3(position.x, position.y, transform.position.z),
                Quaternion.Euler(0f, 0f, rotation));
        }
    }
}
