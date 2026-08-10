using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>
    /// Spare instances of one archetype.
    /// <para>
    /// Deliberately does not track what is active. In a timeline-driven sim the set of live bodies is
    /// derived from history every step and reconciled against what is instantiated, so the pool must
    /// never have an opinion about it — capping or evicting an <i>active</i> instance would silently
    /// contradict the timeline. The cap therefore applies only to idle retention.
    /// </para>
    /// <para>
    /// There is also no timed release. Lifetimes come from recorded loop time, never from a
    /// stopwatch, so nothing here may depend on real seconds.
    /// </para>
    /// </summary>
    public sealed class ArchetypePool
    {
        readonly GameObject _prefab;
        readonly Transform _parent;
        readonly int _maxIdle;
        readonly Stack<GameObject> _idle = new();

        public ArchetypePool(GameObject prefab, Transform parent, int maxIdle)
        {
            _prefab = prefab;
            _parent = parent;
            _maxIdle = Mathf.Max(1, maxIdle);
        }

        public int IdleCount => _idle.Count;
        public int InstantiatedCount { get; private set; }

        /// <summary>
        /// Hands out an instance, still <b>inactive</b>. The caller applies the body's recorded state
        /// and only then activates it: activating first lets <c>OnEnable</c> observe a stale pose,
        /// which for a recording body means its very first sample is wrong.
        /// </summary>
        public GameObject Acquire()
        {
            while (_idle.Count > 0)
            {
                var reused = _idle.Pop();
                if (reused) return reused; // an instance destroyed behind our back is simply dropped
            }
            return Create();
        }

        GameObject Create()
        {
            InstantiatedCount++;
            var created = Object.Instantiate(_prefab, _parent);
            created.SetActive(false);
            return created;
        }

        /// <summary>
        /// Takes an instance back. Beyond the retention cap the surplus is destroyed rather than
        /// hoarded — a loop that spawns thousands of bullets should not keep thousands alive.
        /// </summary>
        public void Release(GameObject instance)
        {
            if (!instance) return;

            instance.SetActive(false);
            if (_idle.Count >= _maxIdle)
            {
                Object.Destroy(instance);
                return;
            }
            _idle.Push(instance);
        }

        /// <summary>
        /// Prewarms the pool so the first loop does not stutter instantiating. Creates directly
        /// rather than going through <see cref="Acquire"/>, which would pop what it just pushed.
        /// </summary>
        public void Prewarm(int count)
        {
            var target = Mathf.Min(count, _maxIdle);
            while (_idle.Count < target) _idle.Push(Create());
        }

        public void Clear()
        {
            while (_idle.Count > 0)
            {
                var instance = _idle.Pop();
                if (instance) Object.Destroy(instance);
            }
        }
    }
}
