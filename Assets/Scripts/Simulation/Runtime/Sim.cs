using System.Collections.Generic;
using UnityEngine;

namespace Chronomancers.Sim
{
    /// <summary>
    /// The cursor, and the loop that drives every body from it.
    ///
    /// There is one time coordinate: the <b>tick</b>. Ticks are ints, they may be negative, and the
    /// loop has no fixed start or end -- how far the player is allowed to travel is a game rule,
    /// not a property of time.
    ///
    /// The cursor is two numbers. <see cref="SimulatedTick"/> is the last tick actually simulated.
    /// <see cref="TargetTick"/> is where the cursor wants to be, and moves by a fraction of a tick
    /// per frame. Each frame we step one tick at a time until the two are within one of each other.
    ///
    /// Physics is driven from here rather than from FixedUpdate, because fast-forward needs several
    /// steps in one frame and Unity's clock will not do that on request. The consequence worth
    /// remembering: <b>no physics runs between steps</b>, so Show is free to move things about
    /// without disturbing anything.
    /// </summary>
    // Low order so Sim.Awake runs before any SimBody enables and tries to register with it.
    [DefaultExecutionOrder(-1000)]
    public class Sim : MonoBehaviour
    {
        public const int TicksPerSecond = 50;
        public const float SecondsPerTick = 1f / TicksPerSecond;

        public static float ToSeconds(int tick) => tick * SecondsPerTick;
        public static int FromSeconds(float seconds) => Mathf.RoundToInt(seconds * TicksPerSecond);

        public static Sim I { get; private set; }

        /// <summary>
        /// The stack of takes and the undo cursor into it. Every recording belongs to one, and
        /// which of them are in force is the only thing undo and redo change.
        /// </summary>
        public Takes Takes { get; } = new Takes();

        /// <summary>
        /// Open a take in which this body re-runs the world from here, and make room for it.
        ///
        /// Dropping the layers above is what makes a redo unreachable: those takes are gone from
        /// the stack, so the recordings filed under their numbers have to go too before anything
        /// writes to the number this take has just been given.
        /// </summary>
        public void OpenTake(SimBody claimant)
        {
            Debug.Assert(claimant.Id.Length > 0,
                claimant.gameObject.name + "has no id, so the take stack cannot tell it from any other body.",
                claimant);

            int take = Takes.Open(claimant.Id, SimulatedTick);

            foreach (var body in bodies)
                body.DropTakesAbove(take - 1);
        }

        /// The last tick that was simulated. Every body describes this tick right now.
        public int SimulatedTick { get; private set; }

        /// Where the cursor is headed, in ticks. Within one tick of SimulatedTick by the end of a
        /// frame, and that leftover fraction is what Show interpolates.
        public float TargetTick { get; private set; }

        /// Ticks per second of real time, signed. 1 is normal, -1 rewinds, 0 freezes, and a fraction
        /// is bullet time. Set by whatever is being watched -- see the superhot rule. Ignored while
        /// the cursor is seeking somewhere of its own accord.
        public float Rate { get; set; } = 1f;

        /// How fast a seek travels, as a multiple of normal speed.
        public const float WindRate = 10f;

        /// <summary>
        /// Whether the cursor is on its way somewhere the player did not steer it -- an undo or a
        /// redo winding to the far end of a take.
        ///
        /// A seek is not a performance. Nothing is claimed while one runs and nothing records, so
        /// it cannot write over the very stretch it is winding through, and it takes no input.
        /// </summary>
        public bool IsWinding { get; private set; }

        int windGoal;
        System.Action onArrival;

        public void WindTo(int tick, System.Action arrived = null)
        {
            windGoal = tick;
            onArrival = arrived;
            IsWinding = true;

            foreach (var body in bodies)
                body.IsSimulated = false;
        }

        /// <summary>
        /// Wind back to where the newest take began, then put it out of force.
        ///
        /// In that order, because the recording being undone is what should play on the way back --
        /// popping it first would leave the bodies with nothing in force covering the ticks being
        /// crossed, and they would sit still instead of un-doing what you watched them do.
        /// </summary>
        public void Undo()
        {
            if (IsWinding || !Takes.CanUndo) return;

            WindTo(Takes.UndoTick, Takes.Undo);
        }

        /// Put the last undone take back in force and wind forward to where it ended. The other way
        /// round from Undo, and for the same reason: the take has to be in force to play.
        public void Redo()
        {
            if (IsWinding || !Takes.CanRedo) return;

            int end = Takes.RedoTick;
            Takes.Redo();
            WindTo(end);
        }

        /// Which way the last step went. Show interpolates across that step, so it needs to know
        /// which tick the step came from. It cannot be derived from Rate's sign: flipping Rate does
        /// not move SimulatedTick, and until a step actually happens the pair Show can safely read
        /// is still the old one.
        int lastStepDir = 1;

        /// How many steps have ever been taken. Show interpolates across the last step, so it needs
        /// two consecutive recorded ticks, and nothing at all is recorded before the first step. So
        /// the first two steps of a session pass without drawing -- during which bodies simply stay
        /// where the scene put them, which is where they belong anyway.
        int stepsTaken;

        readonly List<SimBody> bodies = new List<SimBody>();

        void Awake()
        {
            Debug.Assert(I == null, "Two Sim components in the scene; there must be exactly one.");
            I = this;

            // We call Physics2D.Simulate ourselves, once per tick.
            Physics2D.simulationMode = SimulationMode2D.Script;
            Time.fixedDeltaTime = SecondsPerTick;
        }

        public void Register(SimBody body)
        {
            Debug.Assert(!bodies.Contains(body), $"{body} registered with Sim twice.", body);
            bodies.Add(body);
        }

        public void Unregister(SimBody body) => bodies.Remove(body);

        void Update()
        {
            // Redo first: its binding is the undo one with a modifier, so both fire together.
            if (Controls.Redo) Redo();
            else if (Controls.Undo) Undo();

            if (IsWinding)
                TargetTick = Mathf.MoveTowards(TargetTick, windGoal, WindRate * TicksPerSecond * Time.deltaTime);
            else
                TargetTick += Rate * Time.deltaTime * TicksPerSecond;

            while (TargetTick - SimulatedTick >= 1f) StepOnce(+1);
            while (SimulatedTick - TargetTick >= 1f) StepOnce(-1);

            // After the stepping, not before: the arrival hands the take stack over to its new
            // state, and the last few steps have to have run under the old one.
            if (IsWinding && SimulatedTick == windGoal) Arrive();

            Show();
        }

        void Arrive()
        {
            IsWinding = false;

            var arrived = onArrival;
            onArrival = null;
            arrived?.Invoke();
        }

        /// <summary>
        /// Whether anything is claimed, and so whether the tick being stepped is being re-run under
        /// the live take rather than merely watched.
        ///
        /// Settled once at the top of a step, because it decides which take an unclaimed body files
        /// what it does under, and every body has to agree about that.
        /// </summary>
        public bool IsRerunning { get; private set; }

        bool IsAnythingLive()
        {
            foreach (var body in bodies)
                if (body.IsSimulated) return true;

            return false;
        }

        /// <summary>
        /// Advance the world by exactly one tick, in either direction.
        ///
        /// Physics always steps <i>forwards</i>, whatever dir is. A body recording while the cursor
        /// descends physically moves forwards one step, and we file the result under tick - 1.
        /// Played back later it reads as reversed motion. That is the entire reversal mechanism, and
        /// nothing anywhere has to negate a velocity.
        /// </summary>
        void StepOnce(int dir)
        {
            Debug.Assert(dir == 1 || dir == -1, $"A step is always one tick; got {dir}.");

            int next = SimulatedTick + dir;

            IsRerunning = IsAnythingLive();

            // Sim does not decide who records; each body settles that in PrepareStep and remembers
            // it, so the two sides of the solver cannot disagree.
            foreach (var body in bodies)
                body.PrepareStep(next, dir);

            // Always, even when nothing is recording: MovePosition on a kinematic body only
            // resolves during a step, so without this playback would not move at all.
            Physics2D.Simulate(SecondsPerTick);

            foreach (var body in bodies)
                body.CommitStep();

            // How far the live take has got, which is where redo comes back to.
            if (IsRerunning) Takes.Extend(next);

            SimulatedTick = next;
            lastStepDir = dir;
            stepsTaken++;
        }

        /// <summary>
        /// Place every body where it should appear on screen this frame.
        ///
        /// It interpolates across the <b>last step taken</b>: from the tick the cursor left, to the
        /// tick it is on. Both of those have certainly been simulated
        /// 
        /// The contract for SimBody.Show(fromTick, t): pose yourself at
        /// lerp(state[fromTick], state[fromTick + 1], t).
        /// </summary>
        void Show()
        {
            if (stepsTaken < 2) return;

            // How far the cursor has travelled on from the tick we last simulated, in [0,1).
            float past = Mathf.Abs(TargetTick - SimulatedTick);

            // The last step ran between SimulatedTick and SimulatedTick - lastStepDir. Name that
            // pair in ascending order and point t at the tick we are on, so the body itself never
            // has to know which way time is going.
            int fromTick = lastStepDir > 0 ? SimulatedTick - 1 : SimulatedTick;
            float t = lastStepDir > 0 ? past : 1f - past;

            var show = new SimShow(fromTick, t);

            foreach (var body in bodies)
                body.Show(show);
        }
    }
}
