namespace Chronomancers.Sim
{
    /// <summary>
    /// What a component is told about the step being taken.
    ///
    /// Everything a step could need to know lives here rather than in an argument list, so that
    /// giving components a new fact later does not mean editing every override in the project.
    /// Passed by <c>in</c> and never stored: it describes one step of one body and is stale
    /// immediately afterwards.
    /// </summary>
    public readonly struct SimStep
    {
        /// The tick being stepped to. Anything read or written this step is filed under it.
        public readonly int Tick;

        /// +1 or -1 -- which way the cursor is travelling.
        public readonly int Dir;

        /// Whether this body is writing this tick rather than reading it. Decided once per step by
        /// SimBody, so every component agrees and no component decides for itself.
        public readonly bool IsRecording;

        /// <summary>
        /// The layer to use: the take being written when recording, the take being read when
        /// replaying
        ///
        /// Resolved once by SimBody for the same reason as IsRecording: every component on a body
        /// records together, so they must all read the same layer back.
        /// </summary>
        public readonly int Take;

        /// The tick the cursor came from.
        public int Previous => Tick - Dir;

        /// The tick it reaches next if it keeps going this way.
        public int Next => Tick + Dir;

        public SimStep(int tick, int dir, bool isRecording, int take)
        {
            Tick = tick;
            Dir = dir;
            IsRecording = isRecording;
            Take = take;
        }
    }

    /// <summary>
    /// What a body is told when it is asked to draw itself.
    ///
    /// Separate from SimStep because drawing happens once a frame rather than once a step, and
    /// because it deliberately knows nothing about direction: the pair always ascends, whichever
    /// way the cursor is going. See Sim.Show for why.
    /// </summary>
    public readonly struct SimShow
    {
        /// Lower of the two recorded ticks to interpolate between.
        public readonly int FromTick;

        /// How far between them, in [0,1].
        public readonly float T;

        /// Upper of the two. Always the tick above FromTick.
        public int ToTick => FromTick + 1;

        public SimShow(int fromTick, float t)
        {
            FromTick = fromTick;
            T = t;
        }
    }

    /// <summary>
    /// What a component is handed when it replays: the step, and the state recorded at it.
    ///
    /// A struct rather than two arguments for the same reason as SimStep -- handing Replay one more
    /// fact later should be a change in one place, not in every override in the project.
    /// </summary>
    public readonly struct SimReplay<TState> where TState : struct
    {
        public readonly SimStep Step;

        /// What was recorded at Step.Tick, and so what goes back onto the object.
        public readonly TState State;

        public SimReplay(in SimStep step, in TState state)
        {
            Step = step;
            State = state;
        }
    }

    /// <summary>
    /// What a component is handed when it draws: the two recorded states to blend, and how far
    /// between them.
    /// </summary>
    public readonly struct SimBlend<TState> where TState : struct
    {
        /// Which ticks these two came from. Rarely needed -- mostly for reporting a problem.
        public readonly SimShow Ticks;

        public readonly TState From;
        public readonly TState To;

        /// How far between From and To, in [0,1].
        public float T => Ticks.T;

        public SimBlend(in SimShow ticks, in TState from, in TState to)
        {
            Ticks = ticks;
            From = from;
            To = to;
        }
    }
}
