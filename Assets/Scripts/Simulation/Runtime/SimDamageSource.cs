using UnityEngine;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>
    /// Anything that can deal damage, published so an HP channel can find it.
    /// <para>
    /// Rule 11 requires every damage source to be <b>discoverable at the instant it deals damage</b>, which
    /// is what makes the HP check exact rather than a guess. Nothing is forced to grow a hitbox it would not
    /// otherwise want — a laser can answer with the raycast it already performs — but nothing may deal
    /// damage from nowhere.
    /// </para>
    /// </summary>
    public sealed class SimDamageSource : MonoBehaviour
    {
        [Tooltip("How far away this still counts as discoverable when an HP channel looks for a cause.")]
        public float reach = 0.5f;

        public int damage = 1;

        SimBody _body;

        public SimBody Body => _body != null ? _body : _body = GetComponentInParent<SimBody>();

        void OnEnable()
        {
            if (SimRunner.Instance != null) SimRunner.Instance.Register(this);
        }

        void OnDisable()
        {
            if (SimRunner.Instance != null) SimRunner.Instance.Unregister(this);
        }
    }
}
