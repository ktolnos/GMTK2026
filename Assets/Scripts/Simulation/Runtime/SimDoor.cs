using UnityEngine;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>Discrete. A door is open or shut; there is no such thing as 40% shut.</summary>
    public struct DoorState
    {
        public byte Open;
    }

    /// <summary>
    /// A door, and the canonical divergence check.
    /// <para>
    /// The door is why state has to live on the timeline rather than in a field. Opening a door while
    /// running backwards must not make it shut again in the future — and it does not, because the door's
    /// state is recorded like anything else and the highest <c>Seq</c> covering an instant wins (rule 6).
    /// </para>
    /// <para>
    /// It also owns the check, rather than the bodies it blocks. Rule 11's canonical form is a transform
    /// asking "is my recorded position inside something solid now?", which needs a layer mask tuned so it
    /// does not fire on the floor a body is legitimately standing on. Asking from the door instead — "is
    /// anyone inside me while I am shut?" — is the same question with no tuning and no false positives.
    /// </para>
    /// </summary>
    public sealed class SimDoor : SimulatedComponent<DoorState>, ISimInteractable
    {
        [SerializeField]
        [Tooltip("The collider that blocks passage. Disabled while the door is open.")]
        Collider2D blocker;

        [SerializeField] SpriteRenderer art;
        [SerializeField] Color openTint = new Color(1f, 1f, 1f, 0.25f);

        bool _open;
        bool _pendingToggle;
        Color _shutTint = Color.white;

        public override int ChannelId => SimChannels.Door;

        public bool IsOpen => _open;

        void Awake()
        {
            if (blocker == null) blocker = GetComponent<Collider2D>();
            if (art != null) _shutTint = art.color;
            SetOpen(_open);
        }

        protected override DoorState Capture() => new DoorState { Open = _open ? (byte)1 : (byte)0 };

        protected override void Apply(in Sampled<DoorState> sampled) => SetOpen(sampled.A.Open != 0);

        /// <summary>
        /// Asks to be operated. A door on playback cannot simply change state — it has no span to write
        /// into — so the request claims it first, and the toggle lands on the step after, once the door is
        /// being recorded. That is rule 11 again: one mechanism, and interacting is just another trigger.
        /// </summary>
        public void Interact(SimBody by) => RequestToggle();

        /// <inheritdoc cref="Interact"/>
        public void RequestToggle()
        {
            if (Runner == null) return;
            _pendingToggle = true;
            Runner.RequestClaim(Body.Id, $"{name} was operated");
        }

        internal override void Simulate(LoopTime at, int movedRaw)
        {
            if (!_pendingToggle) return;
            _pendingToggle = false;
            SetOpen(!_open);
        }

        /// <summary>
        /// Claims any played-back body whose recorded path <i>crosses</i> this door while it is shut.
        /// <para>
        /// Swept, not sampled — two earlier attempts were both wrong in instructive ways. Asking "does anything
        /// overlap me" fired on bodies merely standing against the door, because physics reports zero-distance
        /// contact as overlap; and a false claim re-records a body instead of replaying it, which is rewinding
        /// silently ceasing to be pure playback (rule 4). Asking "is any body's centre inside me" fixed that but
        /// tunnels: samples land one per physics step of whoever was recording, so a body recorded at
        /// <c>|rate| &gt; 1</c> can step clean over a narrow doorway with no sample inside it (rule 11b).
        /// </para>
        /// <para>
        /// So the test is the <b>segment between two consecutive recorded samples</b> against the door's
        /// mid-line. The segment cannot tunnel however far apart the samples are, and the mid-line cannot be
        /// touched without actually going through. It also asks the question of the recorded path rather than of
        /// live colliders, which is what the rule is really about.
        /// </para>
        /// </summary>
        protected override void Validate(LoopTime at, in Sampled<DoorState> sampled)
        {
            if (sampled.A.Open != 0 || blocker == null || Runner == null) return; // an open door blocks nobody

            GateLine(out var gateFrom, out var gateTo);

            foreach (var pair in Runner.Live)
            {
                var theirs = pair.Value;
                if (theirs == Body) continue;

                // A recording body is live; the collider simply blocks it. Only *playback* can be impossible —
                // the recording came from a take in which this door was open, and the two were never
                // simultaneously true.
                if (theirs.IsRecording) continue;

                if (!Runner.Timeline.TryGetBody(theirs.Id, out var timeline)) continue;

                var pose = timeline.Sample<PoseState>(SimChannels.Pose, at);
                if (!pose.Exists) continue;

                var from = new Vector2(pose.A.X, pose.A.Y);
                var to = new Vector2(pose.B.X, pose.B.Y);
                if ((to - from).sqrMagnitude < 1e-10f) continue; // stationary between samples: crossed nothing

                if (!Geometry.SegmentsCross(from.x, from.y, to.x, to.y,
                        gateFrom.x, gateFrom.y, gateTo.x, gateTo.y)) continue;

                Runner.RequestClaim(theirs.Id, $"recorded path crosses {name}, which is now shut");
            }
        }

        /// <summary>
        /// The line a body has to cross to get through: along the door's long axis, through its middle. Derived
        /// from the collider, so no orientation has to be configured per door.
        /// </summary>
        void GateLine(out Vector2 from, out Vector2 to)
        {
            var bounds = blocker.bounds;
            var centre = new Vector2(bounds.center.x, bounds.center.y);

            if (bounds.size.y >= bounds.size.x)
            {
                var half = bounds.size.y * 0.5f;
                from = new Vector2(centre.x, centre.y - half);
                to = new Vector2(centre.x, centre.y + half);
            }
            else
            {
                var half = bounds.size.x * 0.5f;
                from = new Vector2(centre.x - half, centre.y);
                to = new Vector2(centre.x + half, centre.y);
            }
        }

        // The crossing test itself lives in Chronomancers.Sim.Geometry, so it can be tested headlessly.

        void SetOpen(bool open)
        {
            _open = open;
            if (blocker != null) blocker.enabled = !open;
            if (art != null) art.color = open ? openTint : _shutTint;
        }
    }
}
