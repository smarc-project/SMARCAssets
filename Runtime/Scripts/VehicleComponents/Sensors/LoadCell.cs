using UnityEngine;

namespace VehicleComponents.Sensors
{
    [RequireComponent(typeof(Joint))]
    public class LoadCell : Sensor
    {
        [Header("LoadCell")]
        public float Force;
        public float Weight;
        Joint joint;

        void Start()
        {
            joint = GetComponent<Joint>();
        }

        public override bool UpdateSensor(double deltaTime)
        {
            Force = joint.currentForce.magnitude;
            Weight = Force / Physics.gravity.magnitude;
            return true;
        }

    }

}