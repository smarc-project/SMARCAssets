using ROS.Core;

namespace VehicleComponents.Actuators
{
    public interface IPercentageActuator : IROSPublishable
    {
        public void SetPercentage(float newValue);
        public float GetResetValue();
        public float GetCurrentValue();
    }
}