using System.Collections.Generic;
using UnityEngine;

namespace Simulation
{
    public class Simulation: MonoBehaviour
    {
        public static Simulation I;
        
        public float time = 0;
        public float timeScale = 1;

        public int universeID = 0;
        
        public Dictionary<string, SimulatedBody> bodies = new();
        public Dictionary<string, HistoryContainer<SimulatedBody.State>> history = new();
        
        
        public void Awake()
        {
            I = this;
        }

        public void RegisterBody(SimulatedBody body)
        {
            bodies.Add(body.id, body);
        }
        
        public void UnregisterBody(SimulatedBody body)
        {
            bodies.Remove(body.id);
        }

        public void Step()
        {
            foreach (var body in bodies.Values)
            {
                if (body.isRecording)
                {
                    history[body.id].Push(body.SaveState());
                }
            }

            foreach (var historyKey in history.Keys)
            {
                
            }
        }
    }
}