using UnityEngine;
using Force;

namespace Smarc.GenericControllers
{
    [AddComponentMenu("Smarc/Generic Controllers/Y-axis Controller")]
    public class YController : MonoBehaviour
    {
        public ArticulationBody RobotAB;
        public Rigidbody RobotRB;
        MixedBody robotBody;

        [Tooltip("Set to 0 to disable force capping")]
        public float MaxForce = 0f;

        [Header("Velocity Controller")]
        public float TargetVelocity = 0f;
        public float MaxSpeed = 10.0f;

        [Header("Identified model — descending powers of z, T = 0.02 s")]
        [SerializeField] double[] b_Gyd = { 0.04243705284439314 };
        [SerializeField] double[] a_Gyd = { 1.0, -0.9580934630482146 };

        public float DroneMass;

        [Header("Debug")]
        public bool DrawDebugRays = true;
        public float DebugRayLength = 3f;
        [Tooltip("Read-only: current heading in degrees. Watch this — if it drifts, you have yaw drift.")]
        public float DebugHeadingDeg;
        [Tooltip("Read-only: angle between commanded force and velocity. ~90 deg => circular motion.")]
        public float DebugForceVelAngleDeg;

        public float ModelVelocity  { get; private set; }
        public float ActualVelocity { get; private set; }
        public Vector3 LastAppliedForce { get; private set; }

        Transform COM;
        DiscreteTf discreteTf;

        void Start()
        {
            if (RobotAB == null && RobotRB == null)
            {
                RobotAB = GetComponentInChildren<ArticulationBody>();
                if (RobotAB == null) RobotRB = GetComponentInChildren<Rigidbody>();
            }

            robotBody = new MixedBody(RobotAB, RobotRB);

            DroneMass = robotBody.GetTotalConnectedMass();

            var globalCom = robotBody.GetTotalConnectedCenterOfMass();
            COM = new GameObject("Y_AxisController_COM").transform;
            COM.parent = robotBody.transform;
            COM.position = globalCom;

            discreteTf = new DiscreteTf(b_Gyd, a_Gyd);
        }

        void FixedUpdate()
        {
            var upDot = Vector3.Dot(robotBody.transform.up, Vector3.up);
            if (upDot < 0.5f)
            {
                Debug.Log($"Robot too tilted for control! upDot: {upDot}");
                return;
            }

            Vector3 enuForward = Vector3.ProjectOnPlane(robotBody.transform.forward, Vector3.up);
            if (enuForward.sqrMagnitude < 1e-6f) return;
            enuForward.Normalize();

            Vector3 enuLeft = Vector3.Cross(enuForward, Vector3.up);
            Debug.Assert(Vector3.Dot(enuLeft, -robotBody.transform.right) > 0.9f, "enuLeft is not left!");

            Vector3 vEnu = robotBody.velocity;
            float vFluY = Vector3.Dot(vEnu, enuLeft);
            ActualVelocity = vFluY;

            float cmd = TargetVelocity;
            if (Mathf.Abs(ActualVelocity) > MaxSpeed) cmd = 0f;

            float vModelY = (float)discreteTf.Step(cmd);
            ModelVelocity = vModelY;

            float forceY = DroneMass * (ModelVelocity - ActualVelocity) / Time.fixedDeltaTime;
            if (MaxForce > 0f) forceY = Mathf.Clamp(forceY, -MaxForce, MaxForce);

            Vector3 forceEnu = enuLeft * forceY;
            robotBody.AddForceAtPosition(forceEnu, COM.position, ForceMode.Force);
            LastAppliedForce = forceEnu;
        }
    }
}