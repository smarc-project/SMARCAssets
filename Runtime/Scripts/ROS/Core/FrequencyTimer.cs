namespace ROS.Core
{
    public class FrequencyTimer
    {
        double lastUpdate = 0f;
        float frequency = 10f;
        float period => 1.0f / frequency;

        /// <summary>
        /// A simple class that simply keeps track of number of periods passed and
        /// provides a way to check if it's time to update.
        /// </summary>
        /// <param name="frequency"></param>
        public FrequencyTimer(float frequency)
        {
            this.frequency = frequency;
        }

        public bool NeedsTick(double now)
        {
            if (frequency <= 0) return true;
            if (lastUpdate == 0f)
            {
                lastUpdate = now;
                return true;
            }
            return now - lastUpdate >= period;
        }

        public void Tick()
        {
            if (frequency <= 0) return;
            lastUpdate += period;
        }

        public bool ExhaustTicks(double now)
        {
            if (frequency <= 0) return true;
            if (lastUpdate == 0f)
            {
                lastUpdate = now;
                return true;
            }
            var timeToTick = now - lastUpdate;
            var numTicks = (int)(timeToTick / period);
            lastUpdate += numTicks * period;
            return numTicks > 0;
        }
    }
}