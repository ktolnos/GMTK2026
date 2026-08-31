using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Chronomancers.Sim
{
    /// <summary>
    /// Routes System.Console into the Unity console.
    ///
    /// The simulation core has no UnityEngine reference, so it reports bugs through
    /// Console.Error. This hooks that up on startup, once, for the whole process -- which also
    /// catches anything else that writes to Console, including third-party code.
    ///
    /// Characters are buffered until a newline so one WriteLine becomes one console entry rather
    /// than one entry per character.
    /// </summary>
    public sealed class UnityTextWriter : TextWriter
    {
        readonly LogType logType;
        readonly StringBuilder pending = new StringBuilder();

        public UnityTextWriter(LogType logType) => this.logType = logType;

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (value == '\n') Flush();
            else if (value != '\r') pending.Append(value);
        }

        public override void Write(string value)
        {
            if (value == null) return;
            foreach (char c in value) Write(c);
        }

        public override void WriteLine(string value)
        {
            Write(value);
            Flush();
        }

        public override void Flush()
        {
            if (pending.Length == 0) return;
            Debug.unityLogger.Log(logType, pending.ToString());
            pending.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            Console.SetOut(new UnityTextWriter(LogType.Log));
            Console.SetError(new UnityTextWriter(LogType.Error));
        }
    }
}
