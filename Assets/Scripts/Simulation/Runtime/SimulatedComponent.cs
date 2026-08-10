using UnityEngine;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>
    /// A component whose state lives on the timeline rather than in its own fields.
    /// <para>
    /// The non-generic base exists so a body can hold a heterogeneous list of them. C# class generics
    /// are not covariant, so <c>List&lt;SimulatedComponent&lt;SomeState&gt;&gt;</c> cannot hold a
    /// component of a different state type — the list has to be typed to a base that does not mention
    /// the state at all, and the state-shaped work has to be virtual calls reached through it.
    /// </para>
    /// <para>
    /// Anything gameplay-relevant belongs in a channel: cooldowns, facing, door state, ammo. State
    /// kept in a plain field survives no rewind, and shows up as a desync the moment the cursor
    /// revisits the instant (rule 12).
    /// </para>
    /// </summary>
    public abstract class SimulatedComponent : MonoBehaviour
    {
        /// <summary>Set by <see cref="SimBody"/> when it caches its components.</summary>
        public SimBody Body { get; internal set; }

        /// <summary>Stable id from <see cref="SimChannels"/>. One per component type.</summary>
        public abstract int ChannelId { get; }

        protected SimRunner Runner => Body != null ? Body.Runner : null;

        /// <summary>Whether this body is being live-simulated rather than played back.</summary>
        protected bool IsRecording => Body != null && Body.IsRecording;

        /// <summary>Direction the cursor is travelling, +1 or -1.</summary>
        protected int Dir => Body != null ? Body.Dir : 1;

        /// <summary>
        /// Whether ordinary contact with a recording body may claim this one (rule 11).
        /// <para>
        /// A projectile says no. Claiming a bullet mid-flight would re-record it in the cursor's
        /// direction and overwrite the end of its span that holds the muzzle, which rule 8 forbids — a
        /// bullet may never appear to have been emitted by the wall it hit. Projectiles handle contact
        /// themselves instead, by stamping a flag.
        /// </para>
        /// </summary>
        internal virtual bool AcceptsContactClaim => true;

        internal abstract void ApplyPlayback(BodyTimeline timeline, LoopTime at);
        internal abstract void CaptureSample(BodyTimeline timeline, int sampleIndex);

        /// <summary>
        /// Live behaviour for a recording body: read intent, apply forces, tick cooldowns. Runs once per
        /// physics step, immediately before <c>Physics2D.Simulate</c>.
        /// <para>
        /// Driven by the runner rather than by Unity's own <c>FixedUpdate</c> on purpose. Component
        /// callback order against the step loop is undefined, so a character that set its velocity in its
        /// own FixedUpdate might run before or after the cursor had moved and playback had been applied —
        /// and which one it got could change between builds.
        /// </para>
        /// <para>
        /// <paramref name="movedRaw"/> is how much loop time this step covered, unsigned. Anything that
        /// counts down — cooldowns, fuses, invulnerability — must use it rather than
        /// <see cref="Time.deltaTime"/>, or it would run at a different speed in bullet time and survive
        /// a rewind unchanged.
        /// </para>
        /// </summary>
        internal virtual void Simulate(LoopTime at, int movedRaw) { }

        /// <summary>
        /// Checks the state just applied against the live world. Runs only on playback bodies, and
        /// only where a state was actually applied — which is why the cursor walks every intervening
        /// instant rather than jumping (rule 11b).
        /// <para>
        /// Implementations do not return a verdict; they call
        /// <see cref="SimRunner.RequestClaim"/> themselves, because the body that has to be claimed is
        /// not always this one. A closed door discovering a body inside it claims <i>the body</i>: the
        /// door is legitimately shut, and it is the recorded path through it that is impossible.
        /// </para>
        /// </summary>
        internal virtual void Validate(LoopTime at) { }

        /// <summary>This body just became recording. Restore anything physics needs to take over.</summary>
        internal virtual void OnClaimed(int dir) { }

        /// <summary>This body just went back to playback.</summary>
        internal virtual void OnReleased() { }

        /// <summary>
        /// This instance has just come out of the pool for a new body. Everything held in a plain field must
        /// be cleared here.
        /// <para>
        /// <c>Awake</c> does not run again on a reused instance, so without this a new bullet inherits the
        /// previous one's fields — including the last sample it played back, which the claim path then
        /// mistakes for momentum to carry and writes over the muzzle velocity it was just given.
        /// </para>
        /// </summary>
        internal abstract void OnAcquired();
    }

    /// <inheritdoc/>
    public abstract class SimulatedComponent<TState> : SimulatedComponent where TState : unmanaged
    {
        Sampled<TState> _last;

        /// <summary>The most recent sample applied to this component, valid on playback bodies.</summary>
        protected Sampled<TState> Last => _last;

        /// <summary>Reads live state into a sample. Called once per physics step while recording.</summary>
        protected abstract TState Capture();

        /// <summary>
        /// Writes a sample onto the live object. <paramref name="sampled"/> carries both bracketing
        /// endpoints and a blend factor rather than one interpolated value, because only this component
        /// knows which of its fields are continuous and which must snap.
        /// </summary>
        protected abstract void Apply(in Sampled<TState> sampled);

        /// <inheritdoc cref="SimulatedComponent.Validate"/>
        /// <param name="at">
        /// The instant being checked. Needed by any check that has to consult <i>other</i> bodies' channels,
        /// and it cannot be read off the clock — the cursor has already been moved to the end of the frame
        /// before the sub-step walk begins, so <c>Clock.Cursor</c> is not where the walk currently is.
        /// </param>
        protected virtual void Validate(LoopTime at, in Sampled<TState> sampled) { }

        /// <summary>Clears this component's own fields for a reused instance. See <see cref="OnAcquired"/>.</summary>
        protected virtual void ResetState() { }

        internal sealed override void OnAcquired()
        {
            _last = default;
            ResetState();
        }

        internal sealed override void ApplyPlayback(BodyTimeline timeline, LoopTime at)
        {
            _last = timeline.Sample<TState>(ChannelId, at);
            if (_last.Exists) Apply(in _last);
        }

        internal sealed override void CaptureSample(BodyTimeline timeline, int sampleIndex) =>
            timeline.Channel<TState>(ChannelId).Set(sampleIndex, Capture());

        internal sealed override void Validate(LoopTime at)
        {
            if (Body != null && Body.IsRecording)
            {
                // A recording body has no sample to check — but a component that polices *other* bodies
                // still has to run, and the case that matters is exactly this one: a door being pulled
                // shut right now, while a body's recorded path still goes through it. So it is handed
                // its own live state as a degenerate sample.
                var live = Capture();
                var synthetic = new Sampled<TState> { Exists = true, A = live, B = live, T = 0f };
                Validate(at, in synthetic);
                return;
            }

            if (_last.Exists) Validate(at, in _last);
        }
    }
}
