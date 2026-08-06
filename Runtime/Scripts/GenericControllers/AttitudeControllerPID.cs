using UnityEngine;

namespace Smarc.GenericControllers
{

    [AddComponentMenu("Smarc/Generic Controllers/Attitude Controller PID")]
    public class AttitudeControllerPID : AttitudeControllerBase
    {
        [Header("Basic PID Attitude Controller")]
        [Tooltip("Acceptable tolerance in degrees")]
        public float YawTolerance = 2.0f;
        
        [Tooltip("If true, the controller will only apply yaw control when the robot is moving (to avoid spinning in place)")]
        public bool OnlyIfMovingForward = false;
        public float TiltKp = 1.5f;


        [Header("Reactive Tilt Settings")]
        public HorizontalController horizontalController;
        public float MaxTiltAngle = 20f;
        public float ExpectedMaxAccel = 10f;
        [Range(-1,1), Tooltip("-1/1 to define the direction of positive pitch/roll, 0 to ignore that axis")]
        public int PitchDirection = 1;
        [Range(-1,1), Tooltip("-1/1 to define the direction of positive pitch/roll, 0 to ignore that axis")]
        public int RollDirection = 1;


        protected override Vector3 GetTargetTiltRate()
        {
            if (TiltMode == TiltMode.ReactToAcceleration)
            {
                if (RollDirection == 0 && PitchDirection == 0)
                {
                    TargetUp = Vector3.up;
                }
                else
                {
                    Vector3 appliedForce = horizontalController.LastAppliedForce;
                    appliedForce.y = 0; // should already be, but just to be safe
                    if(RollDirection != 0 || PitchDirection != 0)
                    {
                        Vector3 localAppliedForce = horizontalController.LastAppliedForceLocal;
                        localAppliedForce.x *= RollDirection;
                        localAppliedForce.z *= PitchDirection;
                        appliedForce = robotBody.transform.TransformVector(localAppliedForce);
                        appliedForce.y = 0;
                    }
                    float mag = appliedForce.magnitude;
                    float targetTiltAngle = 0f;
                    if (mag > 0.05f)
                    {
                        targetTiltAngle = Mathf.Clamp(mag, -ExpectedMaxAccel, ExpectedMaxAccel) / ExpectedMaxAccel * MaxTiltAngle;
                    }
                
                    // keep the target angle within -180 to 180 range
                    if (Mathf.Abs(targetTiltAngle) > 180f) targetTiltAngle -= Mathf.Sign(targetTiltAngle) * 360f;

                    Vector3 tiltAxis = Vector3.Cross(Vector3.up, appliedForce.normalized);
                    Quaternion targetRotation = Quaternion.AngleAxis(targetTiltAngle, tiltAxis);
                    TargetUp = targetRotation * Vector3.up;
                }
            }

            return TiltControl();
        }

        protected override Vector3 GetTargetYawRate()
        {
            if (YawControlMode == YawControlMode.CompassHeading)
            {
                float currentHeading = robotBody.transform.eulerAngles.y;
                // Compute shortest angle difference, so that PID doesn't try to spin the long way around
                float angleDifference = Mathf.DeltaAngle(currentHeading, TargetCompassHeading);
                if (Mathf.Abs(angleDifference) <= YawTolerance) TargetYawRate = 0f;
                else TargetYawRate = Mathf.Sign(angleDifference) * MaxYawRateDeg;
            }

            if (OnlyIfMovingForward)
            {
                if (Mathf.Abs(robotBody.localVelocity.z) < 0.1f) TargetYawRate = 0f;
            }

            return Mathf.Deg2Rad * TargetYawRate * Vector3.up;
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