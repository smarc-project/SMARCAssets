using UnityEngine;
using Force;

namespace Smarc.GenericControllers
{
    [AddComponentMenu("Smarc/Generic Controllers/X-axis Controller")]
    public class XController : MonoBehaviour
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
        [SerializeField] double[] b_Gxd = { 0.03685537260331673 };
        [SerializeField] double[] a_Gxd = { 1.0, -0.9662635006011508 };

        float DroneMass;

        public Vector3 LastAppliedForce { get; private set; }
        public Vector3 LastAppliedForceLocal { get; private set; }

        public float ModelVelocity  { get; private set; }
        public float ActualVelocity { get; private set; }

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
            COM = new GameObject("X_AxisController_COM").transform;
            COM.parent = robotBody.transform;
            COM.position = globalCom;

            discreteTf = new DiscreteTf(b_Gxd, a_Gxd);
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

            Vector3 vEnu = robotBody.velocity;
            float vFluX = Vector3.Dot(vEnu, enuForward);
            ActualVelocity = vFluX;

            float cmd = TargetVelocity;
            if (Mathf.Abs(ActualVelocity) > MaxSpeed) cmd = 0f;

            float vModelX = (float)discreteTf.Step(cmd);
            ModelVelocity = vModelX;

            float forceX = DroneMass * (ModelVelocity - ActualVelocity) / Time.fixedDeltaTime;
            if (MaxForce > 0f) forceX = Mathf.Clamp(forceX, -MaxForce, MaxForce);

            Vector3 forceEnu = enuForward * forceX;
            robotBody.AddForceAtPosition(forceEnu, COM.position, ForceMode.Force);
            LastAppliedForce = forceEnu;
        }
    }
}
