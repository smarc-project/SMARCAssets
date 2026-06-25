using UnityEngine;
using Force;
using System.Collections.Generic;

namespace Smarc.GenericControllers
{
    public enum YawControlMode
    {
        CompassHeading,
        YawRate
    }

    public enum TiltMode
    {
        TargetUp,
        ReactToAcceleration
    }

    /// <summary>
    /// Attitude controller to control yaw and tilt of a robot using angular velocity commands.
    /// </summary>
    [AddComponentMenu("Smarc/Generic Controllers/Attitude Controller")]
    public class AttitudeController : MonoBehaviour
    {
        public ArticulationBody RobotAB;
        public Rigidbody RobotRB;
        private MixedBody robotBody;

        [Tooltip("Acceptable tolerance in degrees")]
        public float YawTolerance = 2.0f;


        [Header("Yaw Rate Controller")]
        public YawControlMode YawControlMode = YawControlMode.CompassHeading;
        public float TargetYawRate = 5.0f; // Target yaw rate in degrees per second
        [Tooltip("If true, the controller will only apply yaw control when the robot is moving (to avoid spinning in place)")]
        public bool OnlyIfMovingForward = false;

        [Header("Compass Heading Controller")]
        public float TargetCompassHeading = 0.0f; // Target heading in degrees
        public float DesiredYawRate = 10f;



        [Header("Tilt Controller")]
        public TiltMode TiltMode = TiltMode.TargetUp;
        [Range(-1,1), Tooltip("-1/1 to define the direction of positive pitch/roll, 0 to ignore that axis")]
        public int PitchDirection = 1;
        [Range(-1,1), Tooltip("-1/1 to define the direction of positive pitch/roll, 0 to ignore that axis")]
        public int RollDirection = 1;
        public Vector3 TargetUp = Vector3.up;
        public float TiltKp = 1.5f;



        [Header("Reactive Tilt Settings")]
        public HorizontalController horizontalController;
        public AltitudeController altitudeController;
        public float MaxTiltAngle = 20f;
        public float ExpectedMaxAccel = 10f;


        public Vector3 LastAppliedTorque { get; private set; }
        public float LastAppliedThrust { get; private set; }


        void Start()
        {
            robotBody = new MixedBody(RobotAB, RobotRB);

            altitudeController = GetComponent<AltitudeController>();
            horizontalController = GetComponent<HorizontalController>();

        }

        void FixedUpdate()
        {
            float upDotLimit = 0.5f;
            var upDot = Vector3.Dot(robotBody.transform.up, Vector3.up);
            Vector3 angVel = Vector3.zero;

            if (TiltMode == TiltMode.ReactToAcceleration && upDot >= upDotLimit)
            {
                if (RollDirection == 0 && PitchDirection == 0)
                {
                    TargetUp = Vector3.up;
                }
                else
                {
                    Vector3 appliedForce = horizontalController.LastAppliedForce;
                    appliedForce += altitudeController.LastAppliedForce;
                    TargetUp = appliedForce.normalized;
                    LastAppliedThrust = appliedForce.magnitude;
                }
            }


            // if the robot is too tilted, just try to upright it first
            if (upDot < upDotLimit) TargetUp = Vector3.up;

            
            angVel += TiltControl();

            
            // check if the robot is upright enough to control the yaw of
            if (upDot < upDotLimit)
            {
                Debug.Log($"Robot too tilted for yaw control! upDot: {upDot}");
                return;
            }

            if (YawControlMode == YawControlMode.CompassHeading)
            {
                float currentHeading = robotBody.transform.eulerAngles.y;
                // Compute shortest angle difference, so that PID doesn't try to spin the long way around
                float angleDifference = Mathf.DeltaAngle(currentHeading, TargetCompassHeading);
                if (Mathf.Abs(angleDifference) <= YawTolerance) TargetYawRate = 0f;
                else TargetYawRate = Mathf.Sign(angleDifference) * DesiredYawRate;
            }

            if (OnlyIfMovingForward)
            {
                if (Mathf.Abs(robotBody.localVelocity.z) < 0.1f) TargetYawRate = 0f;
            }

            
            // so far we just did tilt control, finally add the yaw.
            angVel += Mathf.Deg2Rad * TargetYawRate * Vector3.up;

            // we cant just set the velocity, because physics downstream break when you do... instead we have to add torque to achieve the desired velocity
            Vector3 currentVel = robotBody.angularVelocity;
            Vector3 neededAccel = (angVel - currentVel) / Time.fixedDeltaTime;
            robotBody.AddTorque(neededAccel, ForceMode.Acceleration);
            LastAppliedTorque = neededAccel;

            // robotBody.angularVelocity = angVel;
        }

        Vector3 TiltControl()
        {
            // Angle and axis to rotate current  -> target
            Vector3 currentUp = robotBody.transform.up;
            Vector3 axis = Vector3.Cross(currentUp, TargetUp);

            // Handle nearly-parallel or anti-parallel vectors robustly
            if (axis.sqrMagnitude < 1e-6f)
            {
                float dot = Vector3.Dot(currentUp, TargetUp);
                if (dot > 0f)
                {
                    // Already aligned (or numerically very close)
                    axis = Vector3.zero;
                }
                else
                {
                    // Opposite direction (180°): pick a stable perpendicular axis
                    axis = Vector3.Cross(currentUp, robotBody.transform.forward);
                    if (axis.sqrMagnitude < 1e-6f)
                        axis = Vector3.Cross(currentUp, robotBody.transform.right);

                    // Ensure axis points so a small positive rotation moves currentUp toward TargetUp
                    if (Vector3.Dot(Quaternion.AngleAxis(1f, axis.normalized) * currentUp, TargetUp) <
                        Vector3.Dot(currentUp, TargetUp))
                    {
                        axis = -axis;
                    }
                }
            }
            else
            {
                // Ensure axis direction reduces the angle (choose shortest rotation sign)
                if (Vector3.Dot(Quaternion.AngleAxis(1f, axis.normalized) * currentUp, TargetUp) <
                    Vector3.Dot(currentUp, TargetUp))
                {
                    axis = -axis;
                }
            }
            float axisMag = axis.magnitude;
            if (axisMag < 1e-6f) return Vector3.zero; // already aligned or numerically unstable
            Vector3 correctiveAxis = axis / axisMag;

            float dot2 = Mathf.Clamp(Vector3.Dot(robotBody.transform.up, TargetUp), -1f, 1f);
            float errorDeg = Mathf.Acos(dot2) * Mathf.Rad2Deg;
            Vector3 vel = TiltKp * errorDeg * Mathf.Deg2Rad * correctiveAxis;
            return vel;
        }
        
       
        void OnDrawGizmosSelected()
        {
            // Draw target attitude line
            Gizmos.color = Color.green;
            Transform tf = this.transform;
            if(RobotAB != null)
                tf = RobotAB.transform;
            else if(RobotRB != null)
                tf = RobotRB.transform;
            Vector3 startPos = tf.position;
            Vector3 endPos = tf.position + TargetUp.normalized * 2.0f;
            Gizmos.DrawLine(startPos, endPos);

            // Draw target yaw direction
            if(YawControlMode == YawControlMode.CompassHeading)
            {
                Gizmos.color = Color.blue;
                Vector3 yawDir = Quaternion.Euler(0f, TargetCompassHeading, 0f) * Vector3.forward;
                Vector3 yawEndPos = tf.position + yawDir.normalized * 2.0f;
                Gizmos.DrawLine(startPos, yawEndPos);
            }
            if(YawControlMode == YawControlMode.YawRate)
            {
                Gizmos.color = Color.blue;
                Vector3 yawDir = Quaternion.Euler(0f, tf.eulerAngles.y + TargetYawRate, 0f) * Vector3.forward;
                Vector3 yawEndPos = tf.position + yawDir.normalized * 2.0f;
                Gizmos.DrawLine(startPos, yawEndPos);
            }

        }
    }
}