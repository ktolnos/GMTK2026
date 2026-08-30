using UnityEngine;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>
    /// Which copy this machine emitted, and when it expects its user to have arrived. Sticky once set,
    /// so it is present in every later sample and the check can run at any instant. Both fields discrete.
    /// </summary>
    public struct MachineState
    {
        public int CopyId;
        public int ExpectRaw;
    }

    /// <summary>
    /// The turnstile. Emits an inverted copy of whoever walks in, and afterwards keeps checking that they
    /// really did walk in.
    /// <para>
    /// Its arrival predicate is a plain proximity test, and — this is the part that matters — it is the
    /// <b>same radius</b> that gates entry. If the gate were tighter than the check you would get
    /// explosions for takes that were legal; if it were looser you could step in from outside the check's
    /// range and be doomed no matter what you did. One radius makes the predicate satisfiable by
    /// construction: it asks exactly "did you still come here?".
    /// </para>
    /// <para>
    /// This is also the only check in the game whose failure cannot be repaired by unwinding. By the time
    /// the machine notices, the copy has already run its whole backward pass, which is entirely behind the
    /// cursor — and rule 3 forbids writing there. So the paradox has to be paid for forwards, which is what
    /// the explosion is: a legal, cursor-onward consequence standing in for an illegal retroactive fix.
    /// </para>
    /// </summary>
    public sealed class SimTimeMachine : SimulatedComponent<MachineState>, ISimInteractable
    {
        [SerializeField]
        [Tooltip("ArchetypeRegistry id of the inverted-copy prefab. Its SimRate must be negative.")]
        int copyArchetype = 2;

        [SerializeField]
        [Tooltip("How close a character must be to use the machine — and, later, how close their recorded " +
                 "position must have been for the trip to have happened at all. Deliberately one value.")]
        float range = 1.5f;

        [SerializeField]
        [Tooltip("How far beyond the machine the copy appears, away from whoever walked in. Must clear both " +
                 "bodies' half-widths or they spawn overlapping.")]
        float exitOffset = 1.4f;

        [SerializeField] SpriteRenderer art;
        [SerializeField] Color armedTint = new Color(0.4f, 1f, 0.8f);
        [SerializeField] Color spentTint = new Color(0.35f, 0.4f, 0.45f);

        int _copyId;
        int _expectRaw;
        SimId _pendingUser;
        bool _detonated;

        public override int ChannelId => SimChannels.TimeMachine;

        public bool Used => _copyId != 0;

        void Awake() => Tint();

        protected override MachineState Capture() =>
            new MachineState { CopyId = _copyId, ExpectRaw = _expectRaw };

        protected override void Apply(in Sampled<MachineState> sampled)
        {
            _copyId = sampled.A.CopyId;
            _expectRaw = sampled.A.ExpectRaw;
            Tint();
        }

        // ------------------------------------------------------------------ use

        public void Interact(SimBody by)
        {
            if (Runner == null || by == null || Used || _pendingUser.IsValid) return;
            if (!WithinRange(by.transform.position)) return;

            // A machine on playback has no span to write into, so being used claims it first and the trip
            // happens on the step after (rule 11: one mechanism, another trigger).
            _pendingUser = by.Id;
            Runner.RequestClaim(Body.Id, $"{name} was used by {by.name}");
        }

        internal override void Simulate(LoopTime at, int movedRaw)
        {
            if (!_pendingUser.IsValid || Used) return;

            if (!Runner.Live.TryGetValue(_pendingUser, out var user))
            {
                _pendingUser = SimId.None;
                return;
            }

            // Re-checked at the instant it actually happens: the claim took a step to land, and the
            // character may have walked off in the meantime.
            if (!WithinRange(user.transform.position))
            {
                _pendingUser = SimId.None;
                return;
            }

            // Emitted on the far side, facing back the way the original came.
            //
            // Two instances of one character coexisting is not a paradox (see the turnstile), but spawning
            // them overlapped is still a practical mistake: physics would shove them apart, and since the
            // copy's first sample is captured at this instant, that shove would be written into history as
            // the very thing that starts its recording.
            var here = (Vector2)transform.position;
            var offset = here - (Vector2)user.transform.position;
            var away = offset.sqrMagnitude > 1e-6f ? offset.normalized : Vector2.right;
            var exit = here + away * exitOffset;
            var facing = Mathf.Atan2(-away.y, -away.x) * Mathf.Rad2Deg;

            // Nothing is voided. Read forwards this is a single worldline running backwards into the machine
            // and forwards out of it, so the flip has no mechanism of its own: it is a spawn, exactly like
            // firing a bullet.
            // Control transfers in the same step the copy appears, which is not a convenience — it is what
            // keeps the copy from recording a single step forward before its own negative rate reverses the
            // cursor. That forward step would sit on the far side of its muzzle, and rule 8 would then refuse
            // to let it run backwards at all.
            var id = Runner.RequestSpawn(Body.Id, copyArchetype, exit, facing, Vector2.zero,
                origin: _pendingUser, handControlOver: true);

            _copyId = id.Value;
            _expectRaw = at.Raw;
            _pendingUser = SimId.None;
            Tint();
        }

        // ------------------------------------------------------------------ the check

        protected override void Validate(LoopTime at, in Sampled<MachineState> sampled)
        {
            if (sampled.A.CopyId == 0 || _detonated || Runner == null) return;

            var timeline = Runner.Timeline;
            if (!timeline.TryGetBody(new SimId(sampled.A.CopyId), out var copy)) return;

            var user = copy.Origin;
            if (!user.IsValid || !timeline.TryGetBody(user, out var userTimeline)) return;

            var expect = LoopTime.FromRaw(sampled.A.ExpectRaw);

            // If the user does not exist at all then the copy's origin is gone, and the timeline check has
            // already taken the copy back out of the world (Timeline.OriginHolds) — the cheap necessary
            // condition doing its job. Nothing exploded, and nothing should: an unmade trip is not a paradox.
            if (!userTimeline.Exists(expect)) return;

            var pose = userTimeline.Sample<PoseState>(SimChannels.Pose, expect);
            if (!pose.Exists) return;

            if (WithinRange(new Vector2(pose.A.X, pose.A.Y))) return;

            Detonate($"{name} emitted a copy at {expect}, but its user was " +
                     $"{Vector2.Distance(new Vector2(pose.A.X, pose.A.Y), transform.position):0.00} away");
        }

        /// <summary>
        /// The forward-only consequence. Kills everything that can die, from the cursor onward — which is
        /// legal precisely because it points the way the cursor is already going.
        /// </summary>
        void Detonate(string why)
        {
            _detonated = true;
            Debug.LogError($"paradox: {why}. The ship goes up.", this);

            foreach (var pair in Runner.Live)
                if (pair.Value.GetComponent<SimHealth>() != null)
                    Runner.RequestKill(pair.Key, "paradox: the machine was never reached");
        }

        internal override void OnClaimed(int dir) => _detonated = false;

        bool WithinRange(Vector2 point) =>
            (point - (Vector2)transform.position).sqrMagnitude <= range * range;

        void Tint()
        {
            if (art != null) art.color = Used ? spentTint : armedTint;
        }
    }
}
