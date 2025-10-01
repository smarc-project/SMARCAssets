using UnityEngine;

namespace VehicleComponents.Sensors
{
    public class Leak: Sensor
    {
        [Header("Leak")]
        [Tooltip("Manually set this to trigger a leak.")]
        public bool leaked = false;
        public int count = 0;

        public override bool UpdateSensor(double deltaTime)
        {
            if(leaked) count++;
            return true;
        }
    
    }
}