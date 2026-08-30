using UnityEngine;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>
    /// A bullet. Short-lived, released at a muzzle, and no longer the awkward case for any rule.
    /// <para>
    /// It has no channel of its own. It used to carry an <c>Absorbed</c> flag so that an inverted
    /// character walking into a recorded bullet could stamp "this ended here" without writing behind the
    /// cursor, and a later forward pass would find that flag and retire the tail. All of that is gone:
    /// the contact set on <see cref="SimRigidbody2D"/> notices on the next forward pass that the bullet
    /// meets a body its recording never touched, and diverges it like anything else.
    /// </para>
    /// <para>
    /// It also no longer refuses contact claims. Claiming a bullet mid-flight truncates the end of its
    /// span away from the muzzle, which the old rule 8 forbade — but origin is read forwards, so an
    /// inverted shot read forwards is a bullet leaving a wall and flying <i>into</i> a gun, and a span
    /// that stops short of the muzzle is simply a bullet somebody caught. That is legal and needs no
    /// repair; what breaks in that case is the gun's ammo, and the gun notices it.
    /// </para>
    /// </summary>
    public sealed class SimBullet : MonoBehaviour
    {
        [SerializeField] int damage = 1;

        SimBody _body;

        void Awake() => _body = GetComponentInParent<SimBody>();

        void OnCollisionEnter2D(Collision2D collision) => Hit(collision.collider);

        void OnTriggerEnter2D(Collider2D other) => Hit(other);

        void Hit(Collider2D other)
        {
            if (_body == null || _body.Runner == null || !other) return;

            // Only our own flight deals damage. On playback the recording is the authority on what this
            // bullet did, and a live body that runs into it is claimed by SimBody.Touched instead — which
            // re-simulates the bullet from here, and this fires again for real.
            if (!_body.IsRecording) return;

            var theirs = other.GetComponentInParent<SimBody>();
            if (theirs == _body) return;

            if (theirs != null)
            {
                var health = theirs.GetComponent<SimHealth>();
                if (health != null) health.TakeDamage(damage, _body.Id);
            }

            _body.Runner.RequestKill(_body.Id, "struck something");
        }
    }
}
