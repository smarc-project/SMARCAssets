using UnityEngine;
using ROS.Core;
using Unity.Robotics.Core;

namespace VehicleComponents.Sensors
{

    public class Sensor: LinkAttachment, IROSPublishable
    {
        [Header("Sensor")]
        public float frequency = 10f;
        public bool hasNewData = false;

        protected float Period => 1.0f / frequency;

        
        FrequencyTimer timer;

        protected void OnValidate()
        {
            if (Period < Time.fixedDeltaTime)
            {
                Debug.LogWarning($"[{transform.name}] Sensor update frequency set to {frequency}Hz but Unity updates physics at {1f / Time.fixedDeltaTime}Hz. Setting sensor period to Unity's fixedDeltaTime!");
                frequency = 1f / Time.fixedDeltaTime;
            }
        }

        new protected void Awake()
        {
            base.Awake();
            timer = new FrequencyTimer(frequency);
        }


        public bool HasNewData()
        {
            return hasNewData;
        }


        public virtual bool UpdateSensor(double deltaTime)
        {
            Debug.Log("This sensor needs to override UpdateSensor!");
            return false;
        }

        new void FixedUpdate()
        {
            base.FixedUpdate();
            bool ticked = timer.ExhaustTicks(Clock.Now);
            if (!ticked) return;
            // we dont actually want to do more sensor updates per fixedupdate
            // unlike the ros publisher. since the sensor data would be exactly the same...
            hasNewData = UpdateSensor(Period);
        }

    }
}
