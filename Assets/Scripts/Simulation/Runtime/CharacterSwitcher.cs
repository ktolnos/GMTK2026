using System.Collections.Generic;
using UnityEngine;

namespace Chronomancers.Sim
{
    /// <summary>
    /// Who the player is, and the key that changes it.
    /// 
    /// Switching does not touch anyone's claim. Being watched and writing history are separate
    /// things -- a character you walk away from carries on doing what they were doing.
    /// </summary>
    // Low order so Awake runs before any SimCharacter enables and tries to register.
    [DefaultExecutionOrder(-999)]
    public class CharacterSwitcher : MonoBehaviour
    {
        public static CharacterSwitcher I { get; private set; }

        /// <summary>
        /// Everyone the player could be, in the order they registered.
        /// </summary>
        readonly List<SimCharacter> characters = new List<SimCharacter>();

        /// <summary>
        /// Who the player is. Never null while anyone is registered.
        /// </summary>
        public SimCharacter Controlled { get; private set; }

        void Awake()
        {
            Debug.Assert(I == null, "Two CharacterSwitchers in the scene; there must be exactly one.");
            I = this;
        }

        public void Register(SimCharacter character)
        {
            Debug.Assert(!characters.Contains(character), $"{character} registered twice.", character);
            characters.Add(character);

            if (Controlled == null) Controlled = character;
        }

        public void Unregister(SimCharacter character)
        {
            characters.Remove(character);

            if (Controlled == character)
                Controlled = characters.Count > 0 ? characters[0] : null;
        }

        void Update()
        {
            // A wind is the cursor travelling somewhere of its own accord, and takes no input.
            if (Sim.I.IsWinding) return;

            if (Controls.Next) Switch(+1);
            else if (Controls.Previous) Switch(-1);
        }

        /// Hand control to the character <paramref name="step"/> along the list, wrapping.
        void Switch(int step)
        {
            if (characters.Count == 0) return;

            int from = characters.IndexOf(Controlled);

            // C# modulo keeps the sign of the dividend, so stepping back off the front needs the
            // extra term to land on the last character rather than outside the list.
            Controlled = characters[((from + step) % characters.Count + characters.Count) % characters.Count];
        }
    }
}
