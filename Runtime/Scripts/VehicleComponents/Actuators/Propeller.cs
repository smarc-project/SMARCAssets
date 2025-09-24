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

    public class Propeller : LinkAttachment, IROSPublishable
    {
        [Header("Propeller")]
        public bool reverse = false;
        [Tooltip("Some props are setup with Z axis up, others with Y axis up...")]
        public PropellerOrientation orientation = PropellerOrientation.ZForward;
        public float rpm;
        public float RPMMax = 100000;
        public float RPMMin = 0;
        public float RPMToForceMultiplier = 0.005f;
        public float RPMReverseMultiplier = 0.6f;

        [Header("Drone Propeller")]
        [Tooltip("If set, the propeller will try to hover at a default RPM when started. Assumes the props are all equally distant to the center of mass! If this is not the case, the drone will likely flip around :)")]
        public bool HoverDefault = false;
        public float DefaultHoverRPM;

        [Tooltip("Should the propeller apply manual torque? If unset, the propeller AB will be used to apply torque.")]
        public bool ApplyManualTorque = false;
        [Tooltip("Direction of torque")]
        public bool ManualTorqueUp = false;

        public ArticulationBody baseLinkArticulationBody;
        public Rigidbody baseLinkRigidBody;
        private float c_tau_f = 8.004e-4f;
        private MixedBody baseLinkMixedBody;


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
            //if(hoverdefault) Debug.Log("setting rpm to: " + rpm);
        }

        new void Awake()
        {
            base.Awake();
            baseLinkMixedBody = new MixedBody(baseLinkArticulationBody, baseLinkRigidBody);
            if (HoverDefault) InitializeRPMToStayAfloat();
        }

        new void FixedUpdate()
        {
            base.FixedUpdate();
            if (Physics.simulationMode == SimulationMode.FixedUpdate) DoUpdate();
        }

        public void DoUpdate()
        {
            if (Mathf.Abs(rpm) < RPMMin) rpm = 0;


            float r = rpm * RPMToForceMultiplier * (rpm < 0 ? RPMReverseMultiplier : 1f);

            Vector3 forceDirection = orientation == PropellerOrientation.ZForward ? parentMixedBody.transform.forward : parentMixedBody.transform.up;
            parentMixedBody.AddForceAtPosition(r * forceDirection,
                parentMixedBody.transform.position,
                ForceMode.Force);

            // Dont spin the props (which lets physics handle the torques and such) if we are applying manual
            // torque. This is useful for drones or vehicles where numerical things are known
            // and simulation is not wanted.
            if (ApplyManualTorque)
            {
                int torque_sign = ManualTorqueUp ? 1 : -1;
                float torque = torque_sign * c_tau_f * r;
                Vector3 torqueVector = torque * transform.forward;
                parentMixedBody.AddTorque(torqueVector, ForceMode.Force);
            }
            else
            {
                int direction = reverse ? -1 : 1;
                parentMixedBody.SetDriveTargetVelocity(ArticulationDriveAxis.X, direction * rpm);
            }
        }

        private void InitializeRPMToStayAfloat()
        {
            // Find all child transforms including self
            float totalMass = baseLinkMixedBody.mass;
            var rbs = baseLinkMixedBody.transform.GetComponentsInChildren<Rigidbody>();
            var abs = baseLinkMixedBody.transform.GetComponentsInChildren<ArticulationBody>();

            // Calculate the total mass of all child objects
            foreach (var rb in rbs) totalMass += rb.mass;
            foreach (var ab in abs) totalMass += ab.mass;

            // Calculate the required force to counteract gravity
            float totalRequiredForce = totalMass * Physics.gravity.magnitude;

            // Find all propeller objects under baseLinkMixedBody and calculate their angle from the global vertical axis
            var propellers = baseLinkMixedBody.transform.GetComponentsInChildren<Propeller>();

            // Calculate the required downward force per propeller
            float requiredForcePerProp = totalRequiredForce / propellers.Length;

            // Calculate the required RPM to achieve the desired force
            // Assuming RPMToForceMultiplier is the conversion factor from RPM to force
            // Take into account the orientation of the propeller
            // If the propeller is not aligned with the global up direction, adjust the force
            Vector3 globalUp = Vector3.up;
            Vector3 propForward = orientation == PropellerOrientation.ZForward ? parentMixedBody.transform.forward : parentMixedBody.transform.up;
            float alignment = Mathf.Abs(Vector3.Dot(globalUp.normalized, propForward.normalized));
            if (alignment < 0.01f) alignment = 1f; // Avoid division by zero or too small values
            var ownForce = requiredForcePerProp / alignment;

            float requiredRPM = ownForce / RPMToForceMultiplier;
            DefaultHoverRPM = requiredRPM;

            // Set the initial RPM to each propeller
            SetRpm(requiredRPM);
        }

        public bool HasNewData()
        {
            return true;
        }
    }
}