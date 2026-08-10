using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>
    /// Maps the archetype ids stored in the timeline to the prefabs that realise them.
    /// <para>
    /// Ids are explicit and assigned once, never derived from list position. A saved history refers
    /// to bodies by archetype id, so reordering or inserting entries must not change what an old
    /// save materialises — which position-based indices could not guarantee.
    /// </para>
    /// <para>
    /// Archetype 0 is reserved for bodies authored directly into the scene. They have no prefab, so
    /// they are deactivated rather than destroyed and re-created.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Chronomancers/Archetype Registry", fileName = "ArchetypeRegistry")]
    public sealed class ArchetypeRegistry : ScriptableObject
    {
        /// <summary>Archetype id of a body that lives in the scene and has no prefab.</summary>
        public const int Authored = 0;

        [Serializable]
        public struct Entry
        {
            [Tooltip("Assigned automatically and never reused. Do not edit: saved histories refer to it.")]
            public int id;

            public GameObject prefab;

            [Tooltip("How many spare instances to retain when released. Bullets want many, bosses one.")]
            public int maxIdle;
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();

        readonly Dictionary<int, Entry> _byId = new();
        bool _indexed;

        public IReadOnlyList<Entry> Entries => entries;

        public bool TryGet(int archetype, out Entry entry)
        {
            EnsureIndexed();
            return _byId.TryGetValue(archetype, out entry);
        }

        public GameObject Prefab(int archetype)
        {
            if (archetype == Authored)
                throw new ArgumentException("archetype 0 is an authored scene body and has no prefab",
                    nameof(archetype));
            if (!TryGet(archetype, out var entry) || entry.prefab == null)
                throw new InvalidOperationException(
                    $"archetype {archetype} is not registered; a saved history referring to it cannot be loaded");
            return entry.prefab;
        }

        /// <summary>How many spare instances of this archetype to retain on release.</summary>
        public int MaxIdleFor(int archetype) =>
            TryGet(archetype, out var entry) && entry.maxIdle > 0 ? entry.maxIdle : 16;

        void EnsureIndexed()
        {
            if (_indexed) return;
            _byId.Clear();
            foreach (var entry in entries)
                if (entry.id != Authored)
                    _byId[entry.id] = entry;
            _indexed = true;
        }

        void OnEnable() => _indexed = false;

#if UNITY_EDITOR
        void OnValidate()
        {
            _indexed = false;

            // Hand new rows the next id that has never been used, so ids stay stable under
            // reordering and are never recycled onto a different prefab.
            var next = Authored;
            foreach (var entry in entries) next = Mathf.Max(next, entry.id);

            var seen = new HashSet<int>();
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.id == Authored || !seen.Add(entry.id))
                {
                    entry.id = ++next;
                    seen.Add(entry.id);
                }
                if (entry.maxIdle <= 0) entry.maxIdle = 16;
                entries[i] = entry;

                if (entry.prefab == null)
                    Debug.LogWarning($"{name}: archetype {entry.id} has no prefab", this);
            }
        }
#endif
    }
}
