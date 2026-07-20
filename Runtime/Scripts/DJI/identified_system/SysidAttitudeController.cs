using UnityEngine;
using Force;

namespace Smarc.GenericControllers
{
    [AddComponentMenu("Smarc/Generic Controllers/Sysid Attitude Controller")]
    public class SysidAttitudeController : MonoBehaviour
    {
        public ArticulationBody RobotAB;
        public Rigidbody RobotRB;
        MixedBody robotBody;

        [Header("Heading hold")]
        [Tooltip("Desired compass heading in degrees.")]
        public float TargetHeadingDeg = 0f;
        [Tooltip("Proportional gain, rate per degree of heading error.")]
        public float HeadingKp = 2.0f;
        [Tooltip("Clamp on commanded yaw rate, deg/s.")]
        public float MaxYawRateDeg = 45f;

        [Header("Tilt hold")]
        public Vector3 TargetUp = Vector3.up;
        public float TiltKp = 1.5f;
        [Tooltip("Clamp on commanded tilt rate, deg/s.")]
        public float MaxTiltRateDeg = 90f;

        [Header("Safety")]
        [Tooltip("Below this up-dot, abandon heading control and just right the drone.")]
        public float UpDotLimit = 0.5f;

        [Header("Debug (read-only)")]
        public float DebugHeadingDeg;
        public float DebugHeadingErrorDeg;
        public float DebugTiltErrorDeg;

        void Start()
        {
            robotBody = new MixedBody(RobotAB, RobotRB);
        }

        void FixedUpdate()
        {
            Vector3 currentUp = robotBody.transform.up;
            float upDot = Vector3.Dot(currentUp, Vector3.up);

            Vector3 upTarget = (upDot < UpDotLimit) ? Vector3.up : TargetUp.normalized;

            Vector3 omegaTilt = Vector3.zero;
            float dot = Mathf.Clamp(Vector3.Dot(currentUp, upTarget), -1f, 1f);
            DebugTiltErrorDeg = Mathf.Acos(dot) * Mathf.Rad2Deg;

            Vector3 axis = Vector3.Cross(currentUp, upTarget);
            if (axis.sqrMagnitude < 1e-8f)
            {
                if (dot < 0f)
                {
                    axis = Vector3.Cross(currentUp, robotBody.transform.forward);
                    if (axis.sqrMagnitude < 1e-8f)
                        axis = Vector3.Cross(currentUp, robotBody.transform.right);
                }
            }

            if (axis.sqrMagnitude > 1e-8f)
            {
                float tiltRate = Mathf.Clamp(TiltKp * DebugTiltErrorDeg, -MaxTiltRateDeg, MaxTiltRateDeg);
                omegaTilt = axis.normalized * (tiltRate * Mathf.Deg2Rad);
            }

            Vector3 omegaYaw = Vector3.zero;
            DebugHeadingDeg = robotBody.transform.eulerAngles.y;

            if (upDot >= UpDotLimit)
            {
                DebugHeadingErrorDeg = Mathf.DeltaAngle(DebugHeadingDeg, TargetHeadingDeg);
                float yawRate = Mathf.Clamp(HeadingKp * DebugHeadingErrorDeg, -MaxYawRateDeg, MaxYawRateDeg);
                omegaYaw = Vector3.up * (yawRate * Mathf.Deg2Rad);
            }

            Vector3 omegaDes = omegaTilt + omegaYaw;
            Vector3 omegaNow = robotBody.angularVelocity;
            Vector3 alphaCmd = (omegaDes - omegaNow) / Time.fixedDeltaTime;

            robotBody.AddTorque(alphaCmd, ForceMode.Acceleration);
        }

        void OnDrawGizmosSelected()
        {
            Transform tf = RobotAB != null ? RobotAB.transform
                         : RobotRB != null ? RobotRB.transform
                         : transform;

            Gizmos.color = Color.green;
            Gizmos.DrawLine(tf.position, tf.position + TargetUp.normalized * 2f);

            Gizmos.color = Color.blue;
            Vector3 headingDir = Quaternion.Euler(0f, TargetHeadingDeg, 0f) * Vector3.forward;
            Gizmos.DrawLine(tf.position, tf.position + headingDir * 2f);
        }
    }
}