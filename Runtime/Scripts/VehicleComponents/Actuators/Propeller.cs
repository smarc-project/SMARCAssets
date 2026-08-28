using UnityEngine;
using Force;  // MixedBody is in the Force namespace
using ROS.Core;

namespace VehicleComponents.Actuators
{

    public enum PropellerOrientation
    {
        ZForward,
        YForward
    }

    [AddComponentMenu("Smarc/Actuator/Propeller")]
    public class Propeller : LinkAttachment, IROSPublishable
    {
        [Header("Propeller")]
        [Tooltip("If set, the propeller will spin in the opposite direction wihtout changing the direction of thrust.")]
        public bool reverse_spin = false;
        [Tooltip("If set, the propeller will apply thrust in the opposite direction of the forward setting.")]
        public bool reverse_thrust = false;
        [Tooltip("Some props are setup with Z axis up, others with Y axis up...")]
        public PropellerOrientation orientation = PropellerOrientation.ZForward;
        public float rpm;
        public float RPMMax = 100000;
        public float RPMMin = 0;
        public float RPMToForceMultiplier = 0.005f;
        public float RPMReverseMultiplier = 0.6f;

        [Header("Fake Spin")]
        [Tooltip("If set, the propeller will ONLY visually spin, with the collider. No forces will be applied.")]
        public bool FakeSpin = false;
        public string VisualObjectName = "Visuals";
        public string CollisionObjectName = "Collision";
        Transform propVisual; // for fake spinning.
        Transform propCollision; // for fake spinning.


        [Header("Drone Propeller")]
        [Tooltip("Should the propeller apply manual torque? If unset, the propeller AB will be used to apply torque.")]
        public bool ApplyManualTorque = false;
        [Tooltip("Direction of torque")]
        public bool ReverseManualTorque = false;

        private float c_tau_f = 8.004e-4f;


        void OnValidate()
        {
            // make sure the RPM is within the limits
            if (rpm > RPMMax) rpm = RPMMax;
            if (rpm < -RPMMax) rpm = -RPMMax;
        }

        public void SetRpm(float rpm)
        {
            if (Mathf.Abs(rpm) < RPMMin) rpm = 0;
            this.rpm = Mathf.Clamp(rpm, -RPMMax, RPMMax);
        }

        new void Awake()
        {
            base.Awake();
            if (FakeSpin)
            {
                // find visual and collision children
                propVisual = mixedBody.transform.Find(VisualObjectName);
                propCollision = mixedBody.transform.Find(CollisionObjectName);
            }
        }

        new void FixedUpdate()
        {
            base.FixedUpdate();
            if (Physics.simulationMode == SimulationMode.FixedUpdate) DoUpdate();
        }

        public void DoUpdate()
        {
            if (Mathf.Abs(rpm) < RPMMin) rpm = 0;

            if (FakeSpin)
            {
                int direction = reverse_spin ? -1 : 1;
                Vector3 spinAxis = orientation == PropellerOrientation.ZForward ? direction*Vector3.forward : direction*Vector3.up;
                // just spin the visual and collision objects
                if (propVisual != null)
                {
                    propVisual.Rotate(spinAxis, rpm * 6f * Time.fixedDeltaTime); // 6f to convert RPM to degrees per second
                }
                if (propCollision != null)
                {
                    propCollision.Rotate(spinAxis, rpm * 6f * Time.fixedDeltaTime);
                }
                return;
            }

            float r = rpm * RPMToForceMultiplier * (rpm < 0 ? RPMReverseMultiplier : 1f);
            Vector3 forceDirection = orientation == PropellerOrientation.ZForward ? mixedBody.transform.forward : mixedBody.transform.up;
            forceDirection *= reverse_thrust ? -1f : 1f;

            mixedBody.AddForceAtPosition(r * forceDirection,
                mixedBody.transform.position,
                ForceMode.Force);

            // Dont spin the props (which lets physics handle the torques and such) if we are applying manual
            // torque. This is useful for drones or vehicles where numerical things are known
            // and simulation is not wanted.
            if (ApplyManualTorque)
            {
                int torque_sign = ReverseManualTorque ? 1 : -1;
                float torque = torque_sign * c_tau_f * r;
                Vector3 torqueVector = torque * transform.forward;
                mixedBody.AddTorque(torqueVector, ForceMode.Force);
            }
            else
            {
                int direction = reverse_spin ? -1 : 1;
                mixedBody.SetDriveTargetVelocity(ArticulationDriveAxis.X, direction * rpm);
            }
        }

        public bool HasNewData()
        {
            return true;
        }
    }
}