using UnityEngine;

namespace Chronomancers.Sim
{
    /// <summary>
    /// Someone who can be driven: turns the player's intent into physics, and sets the rate of the
    /// cursor while they are the one being watched.
    ///
    /// A recording character that nobody is driving is <b>inert</b>: no intent, but still physics.
    /// It gets shoved and it can be shot, it just does not act. That is the difference between
    /// recording and being controlled.
    /// </summary>
    [RequireComponent(typeof(SimBody), typeof(Rigidbody2D))]
    public class SimCharacter : SimComponent<SimCharacter.State>
    {
        /// <summary>
        /// Empty for now.
        /// </summary>
        [System.Serializable]
        public struct State { }

        [Tooltip("Metres per second at full stick. Reached instantly -- see the note on inertia.")]
        [SerializeField] float speed = 5f;

        [Header("Time moves when you move")]
        [Tooltip("The superhot rule. Off by default so it does not change the baseline feel.")]
        [SerializeField] bool timeMovesWhenYouMove;

        [Tooltip("Ticks of loop time per second of real time while acting. Negative for a character " +
                 "who experiences the loop backwards.")]
        [SerializeField] float rate = 1f;

        [Tooltip("Magnitude of the rate while standing still. Takes its sign from `rate`, so an " +
                 "inverted character still creeps backwards rather than turning round.")]
        [SerializeField] float idleRate = 0.05f;

        /// <summary>
        /// Whether the player is driving this one.
        ///
        /// Distinct from SimBody.IsSimulated, which is whether history is being written. You
        /// control a character in order to claim it, and you go on controlling it after a backward
        /// seek has dropped the claim -- which is what lets a nudge of the stick take it back.
        /// </summary>
        public bool IsControlled => CharacterSwitcher.I.Controlled == this;

        Rigidbody2D rb;

        void Awake() => rb = GetComponent<Rigidbody2D>();

        void OnEnable() => CharacterSwitcher.I.Register(this);

        void OnDisable()
        {
            if (CharacterSwitcher.I != null) CharacterSwitcher.I.Unregister(this);
        }

        /// <summary>
        /// Loop time per second of real time this character asks for, before the player scrubs.
        ///
        /// Idleness is read live off the controls, so it only tells the truth for the character
        /// being driven. When you can watch someone other than yourself, this has to come from the
        /// recording instead -- which is the first thing that will make this component record.
        /// </summary>
        public float OwnRate =>
            timeMovesWhenYouMove && !Controls.IsActing ? idleRate * Mathf.Sign(rate) : rate;

        /// <summary>
        /// Whether the player is winding the loop the opposite way to how this character lives it.
        ///
        /// Not simply "seeking backwards": a character the reversal machine has turned round
        /// experiences the loop in descending ticks, so it is seeking <i>forwards</i> that fights
        /// them. This is what ends a claim and what stops their own rate counting.
        /// </summary>
        private bool ScrubbingAgainst => Controls.Seek * rate < 0f;

        /// <summary>
        /// What the cursor is asked to do
        /// </summary>
        public float Rate
        {
            get
            {
                float scrub = Controls.Seek * Mathf.Abs(rate);
                float own = ScrubbingAgainst ? 0f : OwnRate;
                float scale = Controls.FastForward ? Controls.FastForwardScale : 1f;

                return (own + scrub) * scale;
            }
        }

        /// <summary>
        /// Drive the cursor, and take the character when the player acts.
        /// </summary>
        void Update()
        {
            // Only the character being driven runs any of this: the claim and the cursor's rate are
            // decisions about the player rather than about a character, and there has to be exactly
            // one of each per frame.
            if (!IsControlled) return;

            // A wind is the cursor moving of its own accord -- an undo or a redo travelling to the
            // far end of a take. It takes no input and claims nothing, or it would record over the
            // stretch it is winding through.
            if (Sim.I.IsWinding) return;
            
            if (!ScrubbingAgainst && Controls.IsActing && !Body.IsSimulated) Claim();

            Sim.I.Rate = Rate;
        }

        /// <summary>
        /// Start writing history with this character.
        /// </summary>
        void Claim()
        {
            // Which way this character lays history down. One the reversal machine has turned
            // round has a negative rate and records while the cursor descends.
            Body.RecordDir = rate < 0f ? -1 : 1;
            Body.IsSimulated = true;
        }

        /// <summary>
        /// What this character is trying to do this tick.
        ///
        /// Zero for anyone the player is not driving, which is the same statement as the driven
        /// character with no keys held -- so a character you walk away from stops dead rather than
        /// skating on. Consistent with there being no inertia anywhere else in the movement model,
        /// and it costs them nothing: being shoved happens inside the step, not through momentum
        /// carried between steps.
        ///
        /// This is where recorded intent goes when there is any to record.
        /// </summary>
        Vector2 Intent => IsControlled ? Controls.Move : Vector2.zero;

        /// Drive the body, before the solver runs.
        protected override void PrepareRecording(in SimStep step)
        {
            rb.linearVelocity = Intent * speed;
        }

        protected override void Replay(in SimReplay<State> replay) { }

        protected override State Capture(in SimStep step) => default;

        protected override void Show(in SimBlend<State> blend) { }
    }
}
