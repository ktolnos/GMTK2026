using UnityEngine;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>
    /// The GameObject side of one simulated body: its identity, its archetype, and its channels.
    /// <para>
    /// A body owns no state of its own. Everything is in its components' channels, so that nothing can
    /// be half-recorded — all of a body's channels share one sample index space and are written on the
    /// same step.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimBody : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Identity of a body authored into the scene. Assigned automatically; never edit it — " +
                 "saved histories refer to it. Left at 0 for prefabs, which are given an id when spawned.")]
        int authoredId;

        [SerializeField]
        [Tooltip("0 for a scene body. Otherwise the ArchetypeRegistry id of the prefab to instantiate.")]
        int archetype = ArchetypeRegistry.Authored;

        SimulatedComponent[] _components;

        public SimId Id { get; private set; }
        public SimRunner Runner { get; private set; }
        public int Archetype => archetype;
        public int AuthoredId => authoredId;
        public SimulatedComponent[] Components => _components;

        /// <summary>Live-simulated rather than played back. Owned by the runner.</summary>
        public bool IsRecording { get; internal set; }

        /// <summary>Direction the cursor was travelling when this body was claimed.</summary>
        public int Dir { get; internal set; } = 1;

        void Awake() => Cache();

        void Cache()
        {
            if (_components != null) return;
            // Children too, so a gun or a hitbox can be its own object under the body.
            _components = GetComponentsInChildren<SimulatedComponent>(true);
            foreach (var component in _components) component.Body = this;
        }

        internal void Bind(SimRunner runner, SimId id)
        {
            Cache();
            Runner = runner;
            Id = id;
        }

        // ------------------------------------------------------------------ contact-driven claims

        void OnCollisionEnter2D(Collision2D collision) => Touched(collision.collider);

        void OnTriggerEnter2D(Collider2D other) => Touched(other);

        /// <summary>
        /// Contact with something already recording claims this body (rule 11) — the same mechanism as
        /// a player takeover, with a different trigger.
        /// <para>
        /// This fires from inside <c>Physics2D.Simulate</c>, so it may only <i>request</i>. Opening a
        /// span here would mutate the timeline while the runner is iterating it, which is exactly the
        /// hazard the deferred command buffer exists for; the claim lands on the next step.
        /// </para>
        /// </summary>
        void Touched(Component other)
        {
            if (Runner == null || IsRecording || !other) return;

            var theirs = other.GetComponentInParent<SimBody>();
            if (theirs == null || theirs == this || !theirs.IsRecording) return;

            // A projectile refuses: re-recording it in the cursor's direction would overwrite the end of
            // its span holding the muzzle. It handles contact itself instead (rule 8).
            foreach (var component in _components)
                if (!component.AcceptsContactClaim) return;

            Runner.RequestClaim(Id, $"touched by recording body {theirs.name}");
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Prefabs are given an id by the runner when spawned, derived from the spawner and the
            // instant, so an authored id would be meaningless — and actively harmful if it collided.
            if (archetype != ArchetypeRegistry.Authored)
            {
                authoredId = 0;
                return;
            }

            if (!gameObject.scene.IsValid()) return; // a prefab asset has no scene peers to compare against

            // Assign on creation, and reassign on duplication. Duplicating a GameObject copies the id
            // too, which would silently merge two bodies' histories into one — so a clash reassigns
            // rather than being left for the runner to trip over at startup.
            //
            // Unity validates the *duplicate*, not the original, so reassigning whenever any peer shares
            // the id is enough and needs no tie-break between the two. If both ever did validate in one
            // pass they would land on the same id, and SimRunner reports that as an error at startup
            // instead of quietly merging their histories.
            var peers = FindObjectsByType<SimBody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var clash = authoredId == 0;
            var highest = 0;
            foreach (var peer in peers)
            {
                if (peer.archetype != ArchetypeRegistry.Authored) continue;
                if (peer.authoredId > highest) highest = peer.authoredId;
                if (peer != this && peer.authoredId == authoredId) clash = true;
            }

            if (clash) authoredId = highest + 1;
        }
#endif
    }
}
