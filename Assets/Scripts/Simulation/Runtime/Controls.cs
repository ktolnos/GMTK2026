using UnityEngine;
using UnityEngine.InputSystem;

namespace Chronomancers.Sim
{
    /// <summary>
    /// The player's hands, read off the project-wide actions asset. See CONTROLS.md.
    ///
    /// Static and pollable at any moment rather than a component with an Update, because input is
    /// set to update in DynamicUpdate: device state is refreshed once at the top of the frame and
    /// everyone who asks afterwards gets the same answer. 
    ///
    /// One consequence worth knowing: fast-forward takes several steps inside a single frame, and
    /// every one of them reads the same input. 
    /// </summary>
    public static class Controls
    {
        static InputAction move, attack, interact, seek, fastForward, undo, redo, next, previous;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bind()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError("No project-wide input actions asset; nothing will respond to input. " +
                               "Set one in Project Settings > Input System Package.");
                return;
            }

            actions.Enable();

            move = Find(actions, "Move");
            attack = Find(actions, "Attack");
            interact = Find(actions, "Interact");
            seek = Find(actions, "Seek");
            fastForward = Find(actions, "FastForward");
            undo = Find(actions, "Undo");
            redo = Find(actions, "Redo");
            next = Find(actions, "Next");
            previous = Find(actions, "Previous");
        }

        /// Resolve once and complain once, rather than returning null and failing every frame from
        /// somewhere that cannot say what is missing.
        static InputAction Find(InputActionAsset actions, string name)
        {
            var action = actions.FindAction(name);
            if (action == null)
                Debug.LogError($"Input action '{name}' is missing from {actions.name}. Add it, or " +
                               $"that control will do nothing for the whole session.");
            return action;
        }

        /// Which way the player is asking their character to walk, magnitude at most 1.
        public static Vector2 Move =>
            Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);

        /// <summary>
        /// Whether the player is doing anything with their character at all.
        ///
        /// One predicate rather than a test against Move, because this is what claims a body and
        /// what makes time move under the superhot rule, and every control that acts on the world
        /// has to count. Firing while standing still is acting.
        ///
        /// Aim is deliberately not one of them: a mouse always has a position, so if pointing
        /// counted the world could never stand still.
        /// </summary>
        public static bool IsActing =>
            Move != Vector2.zero
            || attack.IsPressed()
            || interact.IsPressed();

        /// <summary>
        /// Scrubbing the cursor by hand: negative seeks back through the loop, positive seeks
        /// forward, zero means the player is not scrubbing at all.
        ///
        /// An axis rather than a key so that the magnitude is available -- a stick can scrub at
        /// half speed, where a key can only be held or not.
        ///
        /// What a character makes of it is the character's business -- see SimCharacter.OwnRate.
        /// </summary>
        public static float Seek => seek.ReadValue<float>();

        /// Whether the player is scrubbing at all, either way.
        public static bool IsSeeking => Seek != 0f;

        /// Move through loop time faster, whichever way it is already going.
        public static bool FastForward => fastForward.IsPressed();

        /// How much faster while it is held.
        public const float FastForwardScale = 4f;

        /// Take back the last takeover, and put it back. One-shot, not held.
        ///
        /// Redo is the undo binding with a modifier on it, so pressing redo reports both. Whoever
        /// reads these has to prefer redo.
        public static bool Undo => undo.WasPressedThisFrame();

        public static bool Redo => redo.WasPressedThisFrame();

        /// <summary>
        /// Step to the next character in the list, or the previous one. One-shot, not held.
        /// </summary>
        public static bool Next => next.WasPressedThisFrame();

        public static bool Previous => previous.WasPressedThisFrame();
    }
}
