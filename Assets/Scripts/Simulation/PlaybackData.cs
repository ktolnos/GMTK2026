namespace Simulation
{
    public class PlaybackData<T> where T : SimulationState
    {
        public float time;
        public T previous;
        public T next;
        
        public float t => (next.time - time) / (next.time - previous.time);
    }
}