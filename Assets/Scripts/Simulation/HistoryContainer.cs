using System;

namespace Simulation
{
    public class HistoryContainer<T> where T: SimulationState
    {
        public void Push(T state)
        {
            throw new NotImplementedException();
        }
        
        public PlaybackData<T> GetPlayback(float time, int simulationID)
        {
            throw new NotImplementedException();
        }
        
    }
}