using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>
    /// Drives the whole simulation: moves the one cursor, walks every state it passes, reconciles what
    /// exists, and records the frontier.
    /// <para>
    /// Physics runs in <see cref="SimulationMode2D.Script"/> with exactly one step per frame, at
    /// <see cref="Time.fixedDeltaTime"/>, always. Rate scales the <i>cursor</i>, never physics —
    /// <see cref="Time.timeScale"/> and <c>fixedDeltaTime</c> are never touched (rule 2).
    /// </para>
    /// </summary>
    public sealed class SimRunner : MonoBehaviour
    {
        /// <summary>Backstop against a pathological rate turning one frame into an unbounded walk.</summary>
        const int MaxSubStepsPerFrame = 4096;

        [Header("Loop")]
        [SerializeField] float loopSeconds = 20f;

        [Header("Content")]
        [SerializeField] ArchetypeRegistry archetypes;
        [SerializeField] Transform spawnParent;

        [Header("Controls")]
        [SerializeField] float rewindMultiplier = 3f;
        [SerializeField] float fastMultiplier = 3f;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Log every claim and void with the reason it happened. A claim you did not expect is the " +
                 "main symptom of a rule being implemented wrongly, and it is invisible otherwise.")]
        bool logRepairs = true;

        Timeline _timeline;
        SimClock _clock;
        IIntentSource _input;
        SimIntent _intent;

        readonly Dictionary<SimId, SimBody> _authored = new();
        readonly Dictionary<SimId, SimBody> _live = new();
        readonly Dictionary<int, ArchetypePool> _pools = new();
        readonly List<SimBody> _characters = new();
        readonly List<SimDamageSource> _damageSources = new();

        /// <summary>Bodies with a span currently open.</summary>
        readonly HashSet<SimId> _open = new();

        /// <summary>
        /// Of those, the ones whose span is running latent — out of the world, but still growing so it
        /// keeps outranking whatever older recording says otherwise (rule 7).
        /// </summary>
        readonly HashSet<SimId> _latent = new();

        /// <summary>
        /// Claimed and due to go latent at the next sample. This is the whole of "destruction": there is
        /// no void span to open, only a form to record.
        /// </summary>
        readonly HashSet<SimId> _dying = new();

        readonly List<SimId> _existing = new();
        readonly HashSet<SimId> _existingSet = new();
        readonly List<SimId> _broken = new();
        readonly List<SimId> _scratch = new();
        readonly List<int> _layers = new();

        // Deferred command buffer. Claims, kills and spawns are all requested from inside iteration —
        // collision callbacks fire during Physics2D.Simulate, and validation runs while the runner is
        // walking its body list — so none of them may touch the timeline directly.
        readonly List<PendingClaim> _claims = new();
        readonly List<PendingSpawn> _spawns = new();
        readonly HashSet<SimId> _seededThisStep = new();

        int _dir = 1;
        int _spawnOrdinal;
        bool _paused;
        bool _started;
        float _actingFor;
        bool _actingNow;
        SimId _controlledId;
        SimId _watchedId;
        SimRate _watchedRate;
        SimulationMode2D _restoreSimulationMode;

        // Our own edge state for the discrete keys. See ReadDiscreteControls.
        readonly bool[] _digitHeld = new bool[9];
        bool _spaceHeld, _tabHeld, _undoHeld, _saveHeld, _loadHeld;

        public static SimRunner Instance { get; private set; }

        public Timeline Timeline => _timeline;
        public SimClock Clock => _clock;
        public LoopTime Cursor => _clock.Cursor;
        public int Dir => _dir;
        public bool Paused => _paused;
        /// <summary>
        /// Who the player is driving and whose rate drives the cursor — held as ids, not object references.
        /// <para>
        /// A pooled instance goes back and comes out later as a <i>different</i> body, so a held reference
        /// would eventually steer and time somebody else. And a body can drop out of the live set for a single
        /// step perfectly legitimately, so nulling these on release lost control and silently reverted the rate
        /// to 1 — which read as "time keeps moving forward and nothing responds".
        /// </para>
        /// </summary>
        public SimBody Controlled => _live.TryGetValue(_controlledId, out var body) ? body : null;

        public SimBody Watched => _live.TryGetValue(_watchedId, out var body) ? body : null;

        public SimId ControlledId => _controlledId;
        public SimId WatchedId => _watchedId;
        public IReadOnlyList<SimBody> Characters => _characters;
        public IReadOnlyDictionary<SimId, SimBody> Live => _live;
        public int OpenSpanCount => _open.Count;

        /// <summary>
        /// Whether a physics step will run this frame — which is exactly whether anything is recording
        /// (rule 4: no recording, no physics; a rewind through known territory touches the engine not at
        /// all). Playback poses have to be applied differently either way: swept by
        /// <c>Rigidbody2D.MovePosition</c> when a step is coming to carry it out, and written straight to
        /// <c>Rigidbody2D.position</c> when none is, since a queued move nothing executes would leave
        /// every played-back body standing still.
        /// </summary>
        public bool PhysicsWillStep => _open.Count > 0;
        public int UndoDepth => _layers.Count;

        public bool IsRecording(SimId id) =>
            _open.Contains(id) && !_latent.Contains(id);

        /// <summary>Intent for a body. Everything the player is not holding is inert (rule 11).</summary>
        public SimIntent IntentFor(SimBody body) => body == Controlled ? _intent : default;

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            Instance = this;

            _restoreSimulationMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;

            _timeline = new Timeline();
            SimChannels.RegisterAll(_timeline.Registry);
            _clock = new SimClock(LoopTime.FromSeconds(loopSeconds));

            _input = GetComponent<IIntentSource>() ?? gameObject.AddComponent<PlayerIntentSource>();

            // Reported now rather than on the first spawn, which could be minutes into a playtest.
            if (archetypes == null)
                Debug.LogError($"{name} has no ArchetypeRegistry assigned; nothing can be spawned", this);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Physics2D.simulationMode = _restoreSimulationMode;
        }

        void Start()
        {
            DiscoverAuthoredBodies();

            // The first take: every body claimed at once, almost all of them recording NOOP. This is
            // what gives every body history across the whole loop, which is in turn why rewinding later
            // is pure playback and needs no physics at all (rule 4).
            foreach (var pair in _authored)
            {
                _live[pair.Key] = pair.Value;
                RequestClaim(pair.Key, "first take");
            }

            DrainCommands(_clock.Cursor);

            // Only now pick someone to watch. Taking control opens an undo layer, and the first take must
            // not be inside one — it lands in layer 0, which PopLayer cannot reach, so the original
            // history of the world is not something Ctrl+Z can delete. Claiming first also means this
            // TakeControl finds the body already recording and opens no layer at all.
            if (_characters.Count > 0) TakeControl(_characters[0]);

            _started = true;
        }

        void DiscoverAuthoredBodies()
        {
            var found = FindObjectsByType<SimBody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var body in found)
            {
                if (body.Archetype != ArchetypeRegistry.Authored) continue;

                if (body.AuthoredId == 0)
                {
                    Debug.LogError($"{body.name} has no authored id; it cannot be recorded", body);
                    continue;
                }

                var id = new SimId(body.AuthoredId);
                if (_authored.TryGetValue(id, out var clash))
                {
                    Debug.LogError($"{body.name} and {clash.name} share authored id {body.AuthoredId}", body);
                    continue;
                }

                body.Bind(this, id);
                _authored.Add(id, body);
                _timeline.Declare(id, ArchetypeRegistry.Authored);

                if (body.GetComponent<SimRate>() != null) _characters.Add(body);
            }

            _characters.Sort((a, b) => a.AuthoredId.CompareTo(b.AuthoredId));

            // Scene hazards register themselves in OnEnable, but that may have run before this object's
            // Awake set Instance, so sweep once rather than relying on component order.
            foreach (var source in FindObjectsByType<SimDamageSource>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                Register(source);
        }

        // ------------------------------------------------------------------ the frame

        void Update()
        {
            if (_started) ReadDiscreteControls();
        }

        void FixedUpdate()
        {
            if (!_started) return;

            _intent = _input.Poll();

            // Real seconds on purpose. This gates the *rate*, which is never itself recorded — only its
            // effects are — so it is allowed to depend on the wall clock in a way recorded state never is.
            //
            // The live press is kept separate from the decay window rather than folded into it, so that an
            // activeHoldSeconds of 0 means "no coasting" rather than "never counts as moving at all".
            _actingNow = IntentIsActive(in _intent);
            if (_actingNow) _actingFor = ActiveHoldFor(Watched);
            else _actingFor -= Time.fixedDeltaTime;

            // Rule 11's first trigger: the player touches the controls. Without this a body stays on playback
            // and silently ignores input until its number key is pressed again — which is exactly what happens
            // after a rewind, since reversing closes every span and puts the controlled body back on playback.
            if (Controlled != null && !Controlled.IsRecording && IntentIsActive(in _intent))
                TakeControl(Controlled);

            _clock.Rate = ResolveRate();

            var from = _clock.Cursor;
            var moved = _clock.Advance(Time.fixedDeltaTime);

            if (moved.Raw == 0)
            {
                // Frozen, or clamped at a loop boundary. A frozen cursor records nothing — there is no
                // new loop time to describe — so the world is merely held consistent with where the
                // cursor is, and anything requested meanwhile is dropped rather than written.
                HoldStill();
                return;
            }

            var dir = moved.Raw > 0 ? 1 : -1;
            if (dir != _dir)
            {
                // A span only ever grows in the cursor's direction, so reversing closes every open one
                // and leaves a seam at this instant (rules 3, 6).
                CloseAllSpans();
                _dir = dir;
            }

            var target = _clock.Cursor;

            // Walk every state between here and the target instead of jumping (rule 11b). A check only
            // runs where a state is applied, so a skipped instant is a skipped check — and that leaves
            // an impossible history behind rather than merely a dropped frame.
            var at = from;
            var steps = 0;
            while (_timeline.TryNextChange(at, dir, target, out var next) && next.Raw != target.Raw)
            {
                at = next;
                Visit(at);

                if (++steps < MaxSubStepsPerFrame) continue;
                Debug.LogWarning($"sub-step budget exhausted at {at}; {target} reached by jumping");
                break;
            }

            Visit(target);
            DrainCommands(target);

            // Behaviour, then the spawns it asked for, then physics — all at this one instant, so a
            // bullet's first sample really is at the muzzle it left (rule 8) rather than a step behind.
            DriveRecording(target, Mathf.Abs(moved.Raw));
            DrainSpawns(target);

            // Rule 4: no recording, no physics. After the first take a rewind or a replay touches the
            // physics engine not at all.
            if (_open.Count > 0) Physics2D.Simulate(Time.fixedDeltaTime);

            RecordStep(target);
            ExtendLatent(target);
        }

        /// <summary>Reconcile, apply, then check — the same three things at every instant visited.</summary>
        void Visit(LoopTime at)
        {
            Reconcile(at);
            ApplyPlayback(at);

            // The overlap queries below must see the poses just written. Nothing has stepped physics,
            // so the engine still holds last step's transforms until told otherwise.
            Physics2D.SyncTransforms();

            RunChecks(at);
        }

        void HoldStill()
        {
            Reconcile(_clock.Cursor);
            ApplyPlayback(_clock.Cursor);
            Physics2D.SyncTransforms();

            _claims.Clear();
            _spawns.Clear();
            _dying.Clear();
        }

        float ResolveRate()
        {
            if (_paused) return 0f;

            // Rule 2: rate comes from the *watched* body, which need not be the one being recorded.
            var watched = Watched;

            // Cached, so a body dropping out of the live set for a step does not silently snap the whole world
            // back to rate 1 and reverse the direction it was travelling.
            if (watched != null) _watchedRate = watched.GetComponent<SimRate>();

            var personal = 1f;
            if (_watchedRate != null)
            {
                // Superhot only applies to the body the player is actually driving. A body on playback is
                // always "moving" in the only sense that matters — its recording is running — so watching
                // someone else must not freeze the world just because you are standing still.
                var driving = _watchedId != _controlledId || _actingNow || _actingFor > 0f;
                personal = _watchedRate.Resolve(driving);
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                // Negated rather than forced negative, so R means "against the way this character experiences
                // time" whatever that way is. Watching an inverted copy, R runs the loop forwards again —
                // forcing the sign would have made R a no-op for exactly the bodies that need it most.
                if (keyboard.rKey.isPressed) personal = -personal * rewindMultiplier;

                // Magnitude only, so this speeds up a backward cursor instead of flipping it.
                if (keyboard.leftShiftKey.isPressed) personal *= fastMultiplier;
            }

            return personal;
        }

        /// <summary>
        /// Whether the player is actively driving. Aim is deliberately excluded — a mouse always has a
        /// position, so counting it would mean the world never stands still.
        /// </summary>
        static bool IntentIsActive(in SimIntent intent) =>
            intent.Move.sqrMagnitude > 1e-4f || intent.Fire || intent.Interact;

        static float ActiveHoldFor(SimBody body)
        {
            if (body == null) return 0f;
            var rate = body.GetComponent<SimRate>();
            return rate != null ? Mathf.Max(0f, rate.activeHoldSeconds) : 0f;
        }

        // ------------------------------------------------------------------ existence

        /// <summary>
        /// Brings the instantiated world in line with what the timeline says exists (rule 10). Derived
        /// every step and never accumulated, which is why an arbitrary cursor motion — rewinding, undo,
        /// loading a save — takes exactly the same path as a single forward step.
        /// </summary>
        void Reconcile(LoopTime at)
        {
            _timeline.CollectExisting(at, _existing);
            _existingSet.Clear();
            foreach (var id in _existing) _existingSet.Add(id);

            _scratch.Clear();
            foreach (var pair in _live)
            {
                // A body with an open recorded span *is* the frontier: its authority only widens when it
                // writes this step's sample, at the end of the frame. Asking the timeline whether it exists
                // at a cursor position it has not recorded yet always says no, so leaving this out released
                // every recording body one step after it was claimed.
                if (IsRecording(pair.Key)) continue;
                if (_existingSet.Contains(pair.Key)) continue;

                // Rule 4: beyond the recorded frontier, recording resumes — the body does not stop existing.
                // This is what happens the moment a replay catches up to wherever you last rewound from: no
                // span covers the cursor any more, and without claiming here the entire world is released.
                if (_timeline.Body(pair.Key).AtFrontier(at, _dir))
                {
                    RequestClaim(pair.Key, "the cursor reached the frontier");
                    continue;
                }

                _scratch.Add(pair.Key);
            }

            foreach (var id in _scratch) Release(id);

            foreach (var id in _existing)
            {
                if (_live.ContainsKey(id)) continue;
                var body = Materialize(id);
                if (body == null) continue;

                // Applied twice, deliberately. Before activation so OnEnable does not observe a stale pose —
                // which for a body about to record would make its very first sample wrong. And again after,
                // because the channels that write physics state need a live body to write to; while the
                // object is disabled there is no Rigidbody2D in the engine and those writes are dropped.
                ApplyPlaybackTo(body, at);
                body.gameObject.SetActive(true);
                ApplyPlaybackTo(body, at);
            }
        }

        SimBody Materialize(SimId id)
        {
            var timeline = _timeline.Body(id);

            if (timeline.Archetype == ArchetypeRegistry.Authored)
            {
                if (!_authored.TryGetValue(id, out var authored))
                {
                    Debug.LogWarning($"{id} is an authored body that is not in this scene; skipped");
                    return null;
                }
                _live[id] = authored;
                return authored;
            }

            var instance = PoolFor(timeline.Archetype).Acquire();
            var body = instance.GetComponent<SimBody>();
            if (body == null)
            {
                Debug.LogError($"archetype {timeline.Archetype} has no SimBody", instance);
                return null;
            }

            body.Bind(this, id);

            // A pooled instance is a different body than it was last time. Awake will not run again, so its
            // components have to be told to forget everything they were holding.
            foreach (var component in body.Components) component.OnAcquired();

            _live[id] = body;
            return body;
        }

        /// <summary>
        /// Takes a body's GameObject away. Deliberately leaves <see cref="_open"/> alone: the usual
        /// reason a body leaves the world is that it just went latent, and that span has to keep
        /// growing with the cursor long after there is nothing left to look at (rule 7).
        /// </summary>
        void Release(SimId id)
        {
            if (!_live.TryGetValue(id, out var body)) return;

            // A body that is not in the world cannot be at the recording frontier, so close its span rather
            // than leaking it open forever. Latent spans are the exception the summary above describes.
            if (_open.Contains(id) && !_latent.Contains(id)) CloseSpan(id);

            _live.Remove(id);
            SetRecording(body, false);

            // Controlled/Watched are ids and are deliberately left alone; a body may legitimately drop out of
            // the live set for a step, and losing control over that would be worse than the gap.

            if (body.Archetype == ArchetypeRegistry.Authored) body.gameObject.SetActive(false);
            else PoolFor(body.Archetype).Release(body.gameObject);
        }

        ArchetypePool PoolFor(int archetype)
        {
            if (_pools.TryGetValue(archetype, out var pool)) return pool;
            if (archetypes == null)
                throw new System.InvalidOperationException(
                    $"{name} has no ArchetypeRegistry, so archetype {archetype} cannot be materialised");
            pool = new ArchetypePool(archetypes.Prefab(archetype), spawnParent, archetypes.MaxIdleFor(archetype));
            _pools.Add(archetype, pool);
            return pool;
        }

        // ------------------------------------------------------------------ playback and checks

        void ApplyPlayback(LoopTime at)
        {
            foreach (var pair in _live)
                if (!pair.Value.IsRecording)
                    ApplyPlaybackTo(pair.Value, at);
        }

        void ApplyPlaybackTo(SimBody body, LoopTime at)
        {
            var timeline = _timeline.Body(body.Id);
            foreach (var component in body.Components) component.ApplyPlayback(timeline, at);
        }

        /// <summary>
        /// Runs on every live body, recording or not. Rule 11: two bodies can both be on playback and
        /// still conflict, because their recordings come from different takes and were never
        /// simultaneously true — and a body that is being recorded right now can invalidate someone
        /// else's playback, which is the door case.
        /// </summary>
        void RunChecks(LoopTime at)
        {
            foreach (var pair in _live)
                foreach (var component in pair.Value.Components)
                    component.Validate(at);

            // The one check answerable from history alone (rule 8). A release with nothing behind it did
            // not happen, so the body goes back to being latent — which is a claim like every other
            // repair, not a mechanism of its own.
            _timeline.CollectBrokenOrigins(_broken);
            foreach (var id in _broken)
            {
                // Already out of the world: nothing to repair. Without this, a body whose origin stays
                // broken would be claimed and killed again on every single step, since the origin check
                // has no memory and keeps reporting it.
                if (!_timeline.Exists(id, at)) continue;
                if (SpawnerIsStillCatchingUp(id)) continue;
                RequestKill(id, "whatever released it no longer exists");
            }
        }

        /// <summary>
        /// Whether a spawn's origin looks missing only because the spawner has not written this instant yet.
        /// <para>
        /// Spawns are drained before samples are captured, so at the instant a body is emitted its spawner's
        /// span still ends one step back. The origin check then reads "the spawner did not exist when it
        /// released this" and kills the newborn immediately — which is why the reversal machine's copy
        /// appeared and evaporated in the same breath. The repair is still destructive, so this guard is
        /// still needed.
        /// </para>
        /// <para>
        /// Narrow on purpose: only while the spawner is recording <i>and</i> the origin instant is past the end
        /// of its history, which is precisely the frontier. A spawner that was genuinely latent at that
        /// instant still fails, as it must.
        /// </para>
        /// </summary>
        bool SpawnerIsStillCatchingUp(SimId spawned)
        {
            if (!_timeline.TryGetBody(spawned, out var body)) return false;
            if (!IsRecording(body.Origin)) return false;
            return _timeline.TryGetBody(body.Origin, out var origin) &&
                   origin.AtFrontier(body.OriginAt, _dir);
        }

        // ------------------------------------------------------------------ requests

        public void RequestClaim(SimId id, string reason)
        {
            if (!id.IsValid) return;

            // Already recording: nothing to do. A body running latent still needs claiming — that is
            // exactly what being un-killed looks like — so this may not reject on "has a span open".
            if (_open.Contains(id) && !_latent.Contains(id)) return;

            foreach (var pending in _claims)
                if (pending.Id == id) return;

            _claims.Add(new PendingClaim { Id = id, Reason = reason });
        }

        /// <summary>Destroys a body with no cause, from the cursor onward.</summary>
        /// <summary>
        /// Takes a body out of the world from the next sample onward: it is claimed, and the sample it
        /// records reads <see cref="Form.Latent"/> instead of <see cref="Form.Manifest"/>.
        /// <para>
        /// This is the entirety of destruction. There is no void span, no cause recorded on it, and no
        /// second repair path — a body that should not have died is simply claimed again, and finds
        /// ordinary playback to inherit from because a latent span still carries samples (rule 7).
        /// </para>
        /// <para>
        /// Deferred like every other command: kills are requested from collision callbacks that fire
        /// inside <c>Physics2D.Simulate</c>.
        /// </para>
        /// </summary>
        public void RequestKill(SimId id, string reason)
        {
            if (!id.IsValid) return;
            _dying.Add(id);
            RequestClaim(id, reason);
        }

        /// <summary>
        /// Requests a spawn and returns the id it will have. Deterministic in the spawner and the
        /// instant, so nothing here depends on instantiation order.
        /// </summary>
        /// <param name="origin">
        /// What released the body, if that is not the spawner. They come apart for the reversal machine:
        /// the id derives from the machine, because that is what makes it reproducible, but what released
        /// the copy is the character who walked in. Naming the character means erasing them takes the copy
        /// with it for free via <see cref="Timeline.OriginHolds"/>, without anyone checking geometry.
        /// </param>
        /// <param name="handControlOver">
        /// Hand the player this body the instant it exists, in this same step.
        /// <para>
        /// It has to be this step, not the next. The reversal machine's copy is emitted while the cursor still
        /// runs forward, and its rate is what reverses the cursor — so any delay lets the copy record a forward
        /// tail. That tail is then history on the far side of its muzzle, which rule 8 rightly refuses to grow
        /// backwards across, and the copy is released the moment time reverses.
        /// </para>
        /// </summary>
        public SimId RequestSpawn(SimId spawner, int archetype, Vector2 position, float rotation,
            Vector2 velocity, SimId origin = default, bool handControlOver = false)
        {
            var id = SimId.Spawn(spawner, _clock.Cursor, _spawnOrdinal++);
            _spawns.Add(new PendingSpawn
            {
                Id = id,
                Archetype = archetype,
                Origin = origin.IsValid ? origin : spawner,
                HandControlOver = handControlOver,
                Position = position,
                Rotation = rotation,
                Velocity = velocity,
            });
            return id;
        }

        // ------------------------------------------------------------------ draining

        void DrainCommands(LoopTime at)
        {
            _seededThisStep.Clear();

            foreach (var pending in _claims) ExecuteClaim(pending, at);
            _claims.Clear();
        }

        void DrainSpawns(LoopTime at)
        {
            foreach (var pending in _spawns) ExecuteSpawn(pending, at);
            _spawns.Clear();
        }

        /// <summary>Behaviour for every recording body, immediately before the physics step.</summary>
        void DriveRecording(LoopTime at, int movedRaw)
        {
            foreach (var pair in _live)
            {
                if (!pair.Value.IsRecording) continue;
                foreach (var component in pair.Value.Components) component.Simulate(at, movedRaw);
            }
        }

        void ExecuteClaim(in PendingClaim pending, LoopTime at)
        {
            var wasLatent = _latent.Contains(pending.Id);

            if (_open.Contains(pending.Id))
            {
                // Already recording: nothing to do. Running latent: stop that span growing, because the
                // one about to open outranks it from here on (rule 6).
                if (!wasLatent) return;
                CloseSpan(pending.Id);
            }

            var body = _live.TryGetValue(pending.Id, out var live) ? live : Materialize(pending.Id);
            if (body == null) return;

            if (wasLatent)
            {
                // Being un-killed. Unlike a void there *is* playback here — a latent span carries samples,
                // and the last manifest one before it is what this reads — so rule 5's ordinary inheritance
                // applies with no special case.
                ApplyPlaybackTo(body, at);
                body.gameObject.SetActive(true);
            }

            OpenRecordedSpan(body, at);

            if (logRepairs)
                Debug.Log($"claim {Describe(pending.Id)} at {at} dir {_dir:+0;-0}" +
                          $"{(wasLatent ? " (revived)" : "")}: {pending.Reason}");
        }

        string Describe(SimId id) => _live.TryGetValue(id, out var body) ? $"{body.name} {id}" : id.ToString();

        void ExecuteSpawn(in PendingSpawn pending, LoopTime at)
        {
            _timeline.Declare(pending.Id, pending.Archetype);
            _timeline.DeclareOrigin(pending.Id, pending.Origin, at);

            var body = Materialize(pending.Id);
            if (body == null) return;

            // Pose on the Transform first, so OnEnable does not observe the previous occupant's position.
            body.transform.SetPositionAndRotation(
                new Vector3(pending.Position.x, pending.Position.y, body.transform.position.z),
                Quaternion.Euler(0f, 0f, pending.Rotation));

            body.gameObject.SetActive(true);

            // Physics state only once active. A disabled Rigidbody2D has no body in the engine to write to,
            // so a velocity assigned before this is simply discarded — which is why bullets came out at rest.
            var rigidbody = body.GetComponent<Rigidbody2D>();
            if (rigidbody != null)
            {
                rigidbody.position = pending.Position;
                rigidbody.rotation = pending.Rotation;
                rigidbody.linearVelocity = pending.Velocity;
            }

            // Rule 8: the muzzle is the span's first sample, so it is at one end for a forward shot and
            // at the other for an inverted one. A bullet can never appear to be emitted by a wall.
            OpenRecordedSpan(body, at);

            // Same step, deliberately — see RequestSpawn. The cursor is standing on this instant right now, so
            // handing over here means the next step reverses straight off the muzzle with no tail in between.
            if (pending.HandControlOver) TakeControl(body);
        }

        /// <summary>
        /// Opens a recorded span and immediately writes its first sample from the live object.
        /// <para>
        /// That seed is the whole of rule 5. Playback has just been applied at this instant, so what is
        /// captured here is exactly what playback was showing — old and new authority agree at the
        /// instant they meet, and re-recording cannot teleport anything. Seam-freedom is structural
        /// rather than something to police.
        /// </para>
        /// </summary>
        void OpenRecordedSpan(SimBody body, LoopTime at)
        {
            _timeline.Claim(body.Id, _dir, at);
            _open.Add(body.Id);
            _latent.Remove(body.Id);
            SetRecording(body, true);

            var timeline = _timeline.Body(body.Id);
            var index = timeline.WriteSample(at, Form.Manifest);
            foreach (var component in body.Components) component.CaptureSample(timeline, index);

            _seededThisStep.Add(body.Id);
        }

        void SetRecording(SimBody body, bool recording)
        {
            if (body.IsRecording == recording) return;
            body.IsRecording = recording;
            body.Dir = _dir;

            foreach (var component in body.Components)
            {
                if (recording) component.OnClaimed(_dir);
                else component.OnReleased();
            }
        }

        void RecordStep(LoopTime at)
        {
            _scratch.Clear();

            foreach (var pair in _live)
            {
                var body = pair.Value;
                if (!body.IsRecording) continue;

                var timeline = _timeline.Body(pair.Key);
                if (!timeline.IsRecording) continue;

                // Dying takes precedence over the seed. A body killed on the very step it was claimed —
                // a bullet that spawns already touching a wall — still has to record the sample that puts
                // it out of the world, or its death is silently dropped.
                if (_dying.Remove(pair.Key))
                {
                    // Rule 6: the transition sits inside this span, between a manifest sample and this one,
                    // so it interpolates rather than landing on a boundary. Channels are captured too — the
                    // last thing the body did is what a later claim inherits.
                    var dead = timeline.WriteSample(at, Form.Latent);
                    foreach (var component in body.Components) component.CaptureSample(timeline, dead);

                    _latent.Add(pair.Key);
                    _scratch.Add(pair.Key);
                    continue;
                }

                // Claimed this very step: its seed sample is already at this instant, and it is the
                // pre-physics pose on purpose — that is what makes the seam exact.
                if (_seededThisStep.Contains(pair.Key)) continue;

                var index = timeline.WriteSample(at, Form.Manifest);
                foreach (var component in body.Components) component.CaptureSample(timeline, index);
            }

            // Deferred: Release mutates _live, which is being iterated above.
            foreach (var id in _scratch) Release(id);
        }

        /// <summary>
        /// Grows every latent span to the cursor. A body out of the world has no state worth sampling once
        /// per step, but its span still has to keep outranking the older recording that says it was here —
        /// so it grows by authority alone, over exactly the loop time actually traversed (rule 7).
        /// </summary>
        void ExtendLatent(LoopTime at)
        {
            foreach (var id in _latent)
            {
                var timeline = _timeline.Body(id);
                if (timeline.IsRecording) timeline.Extend(at);
            }
        }

        void CloseSpan(SimId id)
        {
            if (!_open.Remove(id)) return;
            _latent.Remove(id);
            _timeline.Body(id).EndSpan();
            if (_live.TryGetValue(id, out var body)) SetRecording(body, false);
        }

        void CloseAllSpans()
        {
            _scratch.Clear();
            foreach (var id in _open) _scratch.Add(id);
            foreach (var id in _scratch) CloseSpan(id);
        }

        // ------------------------------------------------------------------ damage discoverability

        /// <summary>
        /// Rule 11: an HP channel may only accept a recorded drop it can account for, so every damage
        /// source has to be discoverable at the instant it deals damage. This is the registry half of
        /// that; overlap and raycast are the other halves.
        /// </summary>
        public void Register(SimDamageSource source)
        {
            if (!_damageSources.Contains(source)) _damageSources.Add(source);
        }

        public void Unregister(SimDamageSource source) => _damageSources.Remove(source);

        public bool AnyDamageSourceNear(Vector2 point, float radius, SimBody except)
        {
            foreach (var source in _damageSources)
            {
                if (source == null || !source.isActiveAndEnabled) continue;
                if (source.Body == except) continue;

                var reach = radius + source.reach;
                if (((Vector2)source.transform.position - point).sqrMagnitude <= reach * reach) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ player commands

        public void TakeControl(SimBody body)
        {
            if (body == null) return;

            _controlledId = body.Id;
            _watchedId = body.Id;
            _watchedRate = body.GetComponent<SimRate>();

            // Rule 13: taking control is the only undoable action, because it is the only one that
            // erases something rewinding cannot restore. So it is the only thing that opens a layer.
            if (!body.IsRecording)
            {
                _layers.Add(_timeline.OpenLayer());
                RequestClaim(body.Id, "player takeover");
            }
        }

        public void Undo()
        {
            if (_layers.Count == 0) return;

            var layer = _layers[_layers.Count - 1];
            _layers.RemoveAt(_layers.Count - 1);

            CloseAllSpans();
            _timeline.PopLayer(layer);
            AfterHistoryReplaced();
        }

        public void Save(string path)
        {
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream);
            _timeline.WriteTo(writer);
            Debug.Log($"timeline saved to {path}");
        }

        public void Load(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"no timeline at {path}");
                return;
            }

            CloseAllSpans();
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
                _timeline.ReadFrom(reader);

            AfterHistoryReplaced();
            Debug.Log($"timeline loaded from {path}");
        }

        /// <summary>
        /// Puts the world back in step with a history that changed underneath it. Everything is released
        /// and re-derived rather than patched, which is the same code path a single step takes (rule 10).
        /// </summary>
        void AfterHistoryReplaced()
        {
            _scratch.Clear();
            foreach (var pair in _live) _scratch.Add(pair.Key);
            foreach (var id in _scratch) Release(id);

            _claims.Clear();
            _spawns.Clear();
            _dying.Clear();
            _latent.Clear();

            Reconcile(_clock.Cursor);
            ApplyPlayback(_clock.Cursor);
            Physics2D.SyncTransforms();
        }

        string SavePath => Path.Combine(Application.persistentDataPath, "timeline.chrono");

        /// <summary>
        /// Edges are detected here from <c>isPressed</c> rather than read from
        /// <c>wasPressedThisFrame</c>.
        /// <para>
        /// <c>wasPressedThisFrame</c> is defined against the input system's own update mode. With the project
        /// set to process events in fixed update, a press flagged during a fixed step can be missed entirely by
        /// a <c>Update</c> that runs between steps, or observed twice by two frames inside one step — which is
        /// why pausing worked only sometimes. Comparing against our own previous state cannot disagree with
        /// itself, whatever mode the input system is in.
        /// </para>
        /// </summary>
        void ReadDiscreteControls()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (Pressed(keyboard.spaceKey.isPressed, ref _spaceHeld)) _paused = !_paused;

            if (Pressed(keyboard.tabKey.isPressed, ref _tabHeld) && _characters.Count > 0)
            {
                // Watching is free: it changes the cursor's rate, nothing else. Only *control* records.
                var index = Mathf.Max(0, _characters.IndexOf(Watched));
                var next = _characters[(index + 1) % _characters.Count];
                _watchedId = next.Id;
                _watchedRate = next.GetComponent<SimRate>();
            }

            for (var i = 0; i < _digitHeld.Length; i++)
            {
                var pressed = Pressed(keyboard[Key.Digit1 + i].isPressed, ref _digitHeld[i]);
                if (pressed && i < _characters.Count) TakeControl(_characters[i]);
            }

            var ctrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            if (Pressed(keyboard.zKey.isPressed, ref _undoHeld) && ctrl) Undo();

            if (Pressed(keyboard.f5Key.isPressed, ref _saveHeld)) Save(SavePath);
            if (Pressed(keyboard.f9Key.isPressed, ref _loadHeld)) Load(SavePath);
        }

        static bool Pressed(bool isPressed, ref bool held)
        {
            var edge = isPressed && !held;
            held = isPressed;
            return edge;
        }

        struct PendingClaim
        {
            public SimId Id;
            public string Reason;
        }

        struct PendingSpawn
        {
            public SimId Id;
            public int Archetype;
            public SimId Origin;
            public Vector2 Position;
            public float Rotation;
            public Vector2 Velocity;
            public bool HandControlOver;
        }
    }
}
