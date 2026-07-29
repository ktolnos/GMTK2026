using UnityEngine;

namespace Simulation
{
    public abstract class SimulatedBehaviour<T>: MonoBehaviour where T : SimulationState
    {
        public SimulatedBody body;
        public SimulatedBehaviour<T> prefab;

        private void OnEnable()
        {
           body = GetComponentInParent<SimulatedBody>();
           body.Register(this); 
        }
        
        private void OnDisable()
        {
            body.Unregister(this);
        }

        public abstract T Record(RecordData data);
        
        public abstract T Playback(PlaybackData<T> data);
    }
}