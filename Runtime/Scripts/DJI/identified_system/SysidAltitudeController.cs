using UnityEngine;
using Force;
using DefaultNamespace.Water;

namespace Smarc.GenericControllers
{
    // NOTE: the identified model below (b_Gzd / a_Gzd) was fit from flight data WITHOUT any payload
    // attached. If ExtraMassToCompensate (or any other payload) is used, the extra mass is only
    // accounted for in the gravity compensation term - the model itself has no knowledge of it, so
    // the simulated dynamics will likely diverge noticeably from the real drone's behaviour in that case.
    [AddComponentMenu("Smarc/Generic Controllers/Sysid Altitude Controller")]
    public class SysidAltitudeController : MonoBehaviour
    {
        public ArticulationBody RobotAB;
        public Rigidbody RobotRB;
        MixedBody robotBody;

        public AltitudeControlMode ControlMode = AltitudeControlMode.VerticalVelocity;
        [Tooltip("If true, the controller will only apply altitude control when the robot is moving forward.")]
        public bool OnlyIfMovingForward = false;
        [Tooltip("If true, gravity compensation will be applied before control.")]
        public bool CompensateGravity = true;
        [Min(0), Tooltip("When there is a payload attached, without doing fancy controls. NOTE: the identified model was fit without a payload, so using this will make the simulated behaviour diverge from the real drone.")]
        public float ExtraMassToCompensate = 0f;
        [Tooltip("If true, the COM calculations will include all child rigidbodies/articulation bodies. If your robot is very complex, the controller might behave funny.")]
        public bool IncludeChildrenInCom = false;
        [Tooltip("If true, the mass of all children will be included when computing the mass used for control.")]
        public bool IncludeChildrenInGravityComp = false;

        public float AscentRate = 2.0f;
        public float DescentRate = 2.0f;
        [Tooltip("Set to 0 to disable force capping")]
        public float MaxForce = 0f;

        [Header("Velocity Settings")]
        public float TargetVelocity = 0f;
        public float MaxSpeed = 10.0f;
        [Tooltip("Depending on the vehicle, very low targets can lead to control being dumb. Set to 0 to disable.")]
        public float MinimumDescentTargetVelocity = 0f;
        [Tooltip("Depending on the vehicle, very low targets can lead to control being dumb. Set to 0 to disable.")]
        public float MinimumAscentTargetVelocity = 0f;

        [Header("Position Settings")]
        public float TargetAltitude = 10.0f;
        public float AltitudeTolerance = 0.1f;
        public float GroundLevel = 0f;

        [Header("Identified model — descending powers of z, T = 0.02 s")]
        [Tooltip("Identified WITHOUT a payload attached - see class-level note.")]
        [SerializeField] double[] b_Gzd = { 0.06029777678231629 };
        [SerializeField] double[] a_Gzd = { 1.0, -0.9375490118180505 };

        public float DroneMass;

        public float ModelVelocity  { get; private set; }
        public float ActualVelocity { get; private set; }
        public Vector3 LastAppliedForce { get; private set; }

        Transform COM;
        DiscreteTf discreteTf;
        WaterQueryModel waterModel;

        void Start()
        {
            if (RobotAB == null && RobotRB == null)
            {
                RobotAB = GetComponentInChildren<ArticulationBody>();
                if (RobotAB == null) RobotRB = GetComponentInChildren<Rigidbody>();
            }

            robotBody = new MixedBody(RobotAB, RobotRB);

            DroneMass = robotBody.GetTotalConnectedMass(includeChildren: IncludeChildrenInGravityComp);

            var globalCom = robotBody.GetTotalConnectedCenterOfMass(includeChildren: IncludeChildrenInCom);
            COM = new GameObject("Z_AxisController_COM").transform;
            COM.parent = robotBody.transform;
            COM.position = globalCom;

            discreteTf = new DiscreteTf(b_Gzd, a_Gzd);
        }

        void FixedUpdate()
        {
            var upDot = Vector3.Dot(robotBody.transform.up, Vector3.up);
            if (upDot < 0.5f)
            {
                Debug.Log($"Robot too tilted for control! upDot: {upDot}");
                return;
            }

            if (OnlyIfMovingForward)
            {
                if (Mathf.Abs(robotBody.localVelocity.z) < 0.1f) return;
            }

            if (ControlMode == AltitudeControlMode.AbsoluteAltitude)
            {
                float diff = TargetAltitude - (robotBody.transform.position.y - GroundLevel);
                if (Mathf.Abs(diff) <= AltitudeTolerance) TargetVelocity = 0f;
                else TargetVelocity = Mathf.Sign(diff) * ((diff > 0) ? AscentRate : DescentRate);
            }

            if (ControlMode == AltitudeControlMode.AltitudeFromWater)
            {
                if (waterModel == null) waterModel = WaterQueryModel.GetWaterQueryModel();

                float waterHeight = waterModel.GetWaterLevelAt(robotBody.transform.position);
                float currentAltitudeFromWater = robotBody.transform.position.y - waterHeight;
                float diff = TargetAltitude - currentAltitudeFromWater;
                if (Mathf.Abs(diff) <= AltitudeTolerance) TargetVelocity = 0f;
                else TargetVelocity = Mathf.Sign(diff) * ((diff > 0) ? AscentRate : DescentRate);
            }

            // avoid very low target velocities...
            bool ascending = TargetVelocity > 0f;
            bool descending = TargetVelocity < 0f;
            if (ascending && MinimumAscentTargetVelocity > 0f && TargetVelocity < MinimumAscentTargetVelocity) TargetVelocity = MinimumAscentTargetVelocity;
            if (descending && MinimumDescentTargetVelocity > 0f && TargetVelocity > -MinimumDescentTargetVelocity) TargetVelocity = -MinimumDescentTargetVelocity;

            VelocityControl();
        }

        void VelocityControl()
        {
            ActualVelocity = robotBody.velocity.y;

            float cmd = TargetVelocity;
            if (Mathf.Abs(ActualVelocity) > MaxSpeed) cmd = 0f;

            float vModelZ = (float)discreteTf.Step(cmd);
            ModelVelocity = vModelZ;

            float forceZ = DroneMass * (ModelVelocity - ActualVelocity) / Time.fixedDeltaTime;
            if (CompensateGravity) forceZ += DroneMass * -Physics.gravity.y;
            forceZ += ExtraMassToCompensate * -Physics.gravity.y;
            if (MaxForce > 0f) forceZ = Mathf.Clamp(forceZ, -MaxForce, MaxForce);

            Vector3 forceEnu = Vector3.up * forceZ;

            robotBody.AddForceAtPosition(forceEnu, COM.position, ForceMode.Force);
            LastAppliedForce = forceEnu;
            Debug.DrawRay(COM.position, forceEnu * 0.1f, Color.red);
        }

        void OnDrawGizmosSelected()
        {
            // Draw target altitude line
            Gizmos.color = Color.magenta;
            Transform tf = this.transform;
            if (RobotAB != null)
                tf = RobotAB.transform;
            else if (RobotRB != null)
                tf = RobotRB.transform;
            Vector3 startPos = tf.position;
            Vector3 endPos = new(tf.position.x + 0.1f, GroundLevel + TargetAltitude, tf.position.z + 0.1f);
            Gizmos.DrawLine(startPos, endPos);
        }
    }
}
