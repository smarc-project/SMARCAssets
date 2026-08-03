using UnityEngine;
using Force;

namespace Smarc.GenericControllers
{
    [AddComponentMenu("Smarc/Generic Controllers/Sysid Horizontal Controller")]
    public class SysidHorizontalController : MonoBehaviour
    {
        public ArticulationBody RobotAB;
        public Rigidbody RobotRB;
        MixedBody robotBody;

        [Tooltip("Set to 0 to disable force capping")]
        public float MaxForce = 0f;

        [Header("Velocity Controller — x = forward, y = lateral (left)")]
        public Vector2 TargetVelocity = Vector2.zero;
        public float MaxSpeed = 10.0f;

        [Header("Identified model — descending powers of z, T = 0.02 s")]
        [SerializeField] double[] b_Gxd = { 0.03685537260331673 };
        [SerializeField] double[] a_Gxd = { 1.0, -0.9662635006011508 };

        [SerializeField] double[] b_Gyd = { 0.04243705284439314 };
        [SerializeField] double[] a_Gyd = { 1.0, -0.9580934630482146 };

        public float DroneMass;

        public Vector3 LastAppliedForce { get; private set; }
        public Vector3 LastAppliedForceLocal { get; private set; }
        public Vector2 ModelVelocity  { get; private set; }
        public Vector2 ActualVelocity { get; private set; }

        Transform COM;
        DiscreteTf discreteTfForward, discreteTfLateral;

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
            COM = new GameObject("SysidHorizontalController_COM").transform;
            COM.parent = robotBody.transform;
            COM.position = globalCom;

            discreteTfForward = new DiscreteTf(b_Gxd, a_Gxd);
            discreteTfLateral = new DiscreteTf(b_Gyd, a_Gyd);
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

            Vector3 enuLateral = Vector3.Cross(enuForward, Vector3.up);   // = left
            Debug.Assert(Vector3.Dot(enuLateral, -robotBody.transform.right) > 0.9f, "enuLateral is not left!");

            Vector3 vEnu = robotBody.velocity;

            float vFluForward = Vector3.Dot(vEnu, enuForward);
            float vFluLateral = Vector3.Dot(vEnu, enuLateral);
            ActualVelocity = new Vector2(vFluForward, vFluLateral);

            float cmdForward = TargetVelocity.x;
            float cmdLateral = TargetVelocity.y;
            if (Mathf.Abs(vFluForward) > MaxSpeed) cmdForward = 0f;
            if (Mathf.Abs(vFluLateral) > MaxSpeed) cmdLateral = 0f;

            float vModelForward = (float)discreteTfForward.Step(cmdForward);
            float vModelLateral = (float)discreteTfLateral.Step(cmdLateral);
            ModelVelocity = new Vector2(vModelForward, vModelLateral);

            float forceForward = DroneMass * (vModelForward - vFluForward) / Time.fixedDeltaTime;
            float forceLateral = DroneMass * (vModelLateral - vFluLateral) / Time.fixedDeltaTime;

            if (MaxForce > 0f)
            {
                forceForward = Mathf.Clamp(forceForward, -MaxForce, MaxForce);
                forceLateral = Mathf.Clamp(forceLateral, -MaxForce, MaxForce);
            }

            Vector3 forceForwardEnu = enuForward * forceForward;
            Vector3 forceLateralEnu = enuLateral * forceLateral;
            Vector3 totalHorizontalForce = forceForwardEnu + forceLateralEnu;

            robotBody.AddForceAtPosition(totalHorizontalForce, COM.position, ForceMode.Force);

            LastAppliedForce = totalHorizontalForce;
            LastAppliedForceLocal = robotBody.transform.InverseTransformDirection(totalHorizontalForce);
        }
    }
}