using UnityEngine;
using Force;

namespace Smarc.GenericControllers
{
    [AddComponentMenu("Smarc/Generic Controllers/Z-axis Controller")]
    public class ZController : MonoBehaviour
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
        [SerializeField] double[] b_Gzd = { 0.06029777678231629 };
        [SerializeField] double[] a_Gzd = { 1.0, -0.9375490118180505 };

        
        public float DroneMass;

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

            ActualVelocity = robotBody.velocity.y;

            float cmd = TargetVelocity;
            if (Mathf.Abs(ActualVelocity) > MaxSpeed) cmd = 0f;

            float vModelZ = (float)discreteTf.Step(cmd);
            ModelVelocity = vModelZ;

            float forceZ = DroneMass * (ModelVelocity - ActualVelocity) / Time.fixedDeltaTime;
            if (MaxForce > 0f) forceZ = Mathf.Clamp(forceZ, -MaxForce, MaxForce);

            Vector3 forceEnu = Vector3.up * forceZ;
            forceEnu -= Physics.gravity * DroneMass;      

            robotBody.AddForceAtPosition(forceEnu, COM.position, ForceMode.Force);
            LastAppliedForce = forceEnu;
        }
    }
}