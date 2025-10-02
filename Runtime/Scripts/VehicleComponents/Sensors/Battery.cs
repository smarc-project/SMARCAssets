using UnityEngine;

namespace VehicleComponents.Sensors
{
    public class Battery: Sensor
    {
        [Header("Battery")]
        public float dischargePercentPerMinute = 1;
        public float currentPercent = 95f;
        public float maxVoltage = 25.2f; // default sam
        public float minVoltage = 21f; // default sam
        public float currentVoltage = 25.2f;

        float Vdiff => maxVoltage - minVoltage;

        public override bool UpdateSensor(double deltaTime)
        {
            currentPercent -= (float) (deltaTime/60 * dischargePercentPerMinute);
            if(currentPercent < 0f) currentPercent = 0f;
            currentVoltage = minVoltage + (Vdiff * currentPercent / 100f);
            return true;
        }
    
    }

}