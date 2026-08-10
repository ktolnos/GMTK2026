using UnityEngine;
using UnityEngine.InputSystem;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>
    /// Top-down keyboard-and-mouse intent for whichever body the player currently controls: WASD to move,
    /// mouse to aim, left button to fire.
    /// <para>
    /// Reads <see cref="Keyboard"/> and <see cref="Mouse"/> directly rather than an action asset so the
    /// playtest scene needs no wiring. The project is set to the new input backend only, so
    /// <c>UnityEngine.Input</c> is not compiled in and is not an option here.
    /// </para>
    /// </summary>
    public sealed class PlayerIntentSource : MonoBehaviour, IIntentSource
    {
        public SimIntent Poll()
        {
            var intent = default(SimIntent);

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                var move = Vector2.zero;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;

                intent.Move = move;
                intent.Fire = keyboard.jKey.isPressed;
                intent.Interact = keyboard.eKey.isPressed || keyboard.kKey.isPressed;
            }

            var mouse = Mouse.current;
            var camera = Camera.main;
            if (mouse != null && camera != null)
            {
                // The aim is passed as a world *point*, not a direction, so this stays ignorant of which
                // body it is steering. The character turns it into an angle relative to itself.
                var screen = mouse.position.ReadValue();
                var world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
                intent.Aim = new Vector2(world.x, world.y);
                intent.HasAim = true;

                if (mouse.leftButton.isPressed) intent.Fire = true;
            }

            return intent;
        }
    }
}
