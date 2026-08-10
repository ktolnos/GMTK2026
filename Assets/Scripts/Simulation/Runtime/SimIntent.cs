using UnityEngine;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>
    /// What a character is trying to do this step. Never recorded — only its effects are, which is why it
    /// may hold view-layer things like a mouse position without that leaking into history.
    /// </summary>
    public struct SimIntent
    {
        /// <summary>Desired movement, both axes. Top-down, so there is no jump.</summary>
        public Vector2 Move;

        /// <summary>World point being aimed at. Only meaningful when <see cref="HasAim"/>.</summary>
        public Vector2 Aim;

        public bool HasAim;
        public bool Fire;
        public bool Interact;
    }

    /// <summary>
    /// Where a claimed character's intent comes from.
    /// <para>
    /// The default is <c>default(SimIntent)</c> — no input at all. That is what makes a claimed body
    /// <i>inert rather than dead</i> (rule 11): it has no intent, but it still has physics. It keeps its
    /// momentum, gets pushed and can die. The first take is every body claimed with this source, almost all
    /// of them doing nothing.
    /// </para>
    /// <para>
    /// Implementations must live in their own file — see <see cref="PlayerIntentSource"/>.
    /// </para>
    /// </summary>
    public interface IIntentSource
    {
        SimIntent Poll();
    }

    /// <summary>Something a character can operate by standing near it and pressing interact.</summary>
    public interface ISimInteractable
    {
        void Interact(SimBody by);
    }
}
