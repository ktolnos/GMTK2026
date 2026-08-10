using UnityEngine;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>
    /// How fast this body experiences the loop. When it is the <i>watched</i> body this becomes the global
    /// cursor rate, which is the whole of rule 2 — bullet time and inverted characters are the same field
    /// with different values, and neither touches physics.
    /// <para>
    /// In its own file because Unity resolves a MonoBehaviour's scene reference by matching the class name
    /// to the file name. A MonoBehaviour sharing a file with another type is silently dropped from the
    /// scene on load as a missing script.
    /// </para>
    /// </summary>
    public sealed class SimRate : MonoBehaviour
    {
        [Tooltip("Loop-seconds per real second while watched. 0.2 is bullet time; -1 runs backwards.")]
        public float rate = 1f;

        [Tooltip("Superhot: the world only moves while this character does. Applies while the player is " +
                 "driving this body and watching it.")]
        public bool timeMovesWhenYouMove;

        [Tooltip("Rate while standing still, if timeMovesWhenYouMove. Signed like rate, so an inverted " +
                 "character still creeps backwards. Exactly 0 freezes the cursor and records nothing at all, " +
                 "which is legal but means a motionless character accrues no history to replay.")]
        public float idleRate = 0.05f;

        [Tooltip("How long after a control press the character still counts as moving. Smooths the " +
                 "stop-start; real seconds, which is fine because rate is never itself recorded.")]
        public float activeHoldSeconds = 0.15f;

        /// <summary>The rate this body should drive the cursor at, given whether it is currently acting.</summary>
        public float Resolve(bool acting)
        {
            if (!timeMovesWhenYouMove || acting) return rate;

            // Signed off `rate` rather than used raw, so reversing a character reverses its idle creep too.
            return rate < 0f ? -Mathf.Abs(idleRate) : Mathf.Abs(idleRate);
        }
    }
}
