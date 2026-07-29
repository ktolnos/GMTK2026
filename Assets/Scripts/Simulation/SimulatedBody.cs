using System;
using System.Collections.Generic;
using UnityEngine;

namespace Simulation
{
    public class SimulatedBody: MonoBehaviour, ISerializationCallbackReceiver
    {
        public SimulatedBody prefab;
        public bool isRecording;
        public float timeScale;
        
        public string id;
        public float creationTime;
        public float destructionTime;
        
        private List<SimulatedBehaviour<SimulationState>> behaviours;
        private void OnEnable()
        {
            Simulation.I.RegisterBody(this);
        }

        private void OnDisable()
        {
            Simulation.I.UnregisterBody(this);
        }
        
        public void Register<T>(SimulatedBehaviour<T> simulatedBehaviour) where T : SimulationState
        {
            
        }
        
        public void Unregister<T>(SimulatedBehaviour<T> simulatedBehaviour) where T : SimulationState
        {
            
        }
        
        public void OnBeforeSerialize()
        {
            // Generate an ID if it doesn't exist yet and we are in the Editor
            if (string.IsNullOrEmpty(id) && !Application.isPlaying)
            {
                id = Guid.NewGuid().ToString();
            }
        }

        public void OnAfterDeserialize()
        {
            // NOOP
        }

        public void SetState(PlaybackData<State> data)
        {
            
        }
        
        public State SaveState()
        {
            return new State(); // TODO
        }

        public class State : SimulationState
        {
            
        }
    }
}