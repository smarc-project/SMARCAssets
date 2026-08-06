using UnityEngine;
using Smarc.GenericControllers;

namespace dji
{
    [AddComponentMenu("Smarc/DJI/Sysid Horizontal Controller")]
    public class SysidHorizontalController : HorizontalControllerBase
    {

        // [Header("Velocity Controller — x = forward, y = lateral (left)")]
        // public Vector2 TargetVelocity = Vector2.zero;

        [Header("Identified model — descending powers of z, T = 0.02 s")]
        [SerializeField] double[] b_Gxd = { 0.03685537260331673 };
        [SerializeField] double[] a_Gxd = { 1.0, -0.9662635006011508 };

        [SerializeField] double[] b_Gyd = { 0.04243705284439314 };
        [SerializeField] double[] a_Gyd = { 1.0, -0.9580934630482146 };

        float DroneMass;


        public Vector2 ModelVelocity  { get; private set; }
        public Vector2 ActualVelocity { get; private set; }

        DiscreteTf discreteTfForward, discreteTfLateral;

        new void Start()
        {
            base.Start();
            DroneMass = robotBody.GetTotalConnectedMass();
            discreteTfForward = new DiscreteTf(b_Gxd, a_Gxd);
            discreteTfLateral = new DiscreteTf(b_Gyd, a_Gyd);
        }

        protected override Vector3 GetHorizontalForceLocal()        
        {
            Vector3 enuForward = Vector3.ProjectOnPlane(robotBody.transform.forward, Vector3.up);
            if (enuForward.sqrMagnitude < 1e-6f) return Vector3.zero;
            enuForward.Normalize();

            // all these "enu-X" things are not _really_ east-north-up, they just mean
            // they are aligned with the horizontal plane of the world regardless of the robots tilt etc.
            // enuForward is the forward direction of the robot projected onto the horizontal plane
            // enuLeft is the left direction of the robot projected onto the horizontal plane
            Vector3 enuLeft = Vector3.Cross(enuForward, Vector3.up);   // = left
            Debug.Assert(Vector3.Dot(enuLeft, -robotBody.transform.right) > 0.9f, "enuLeft is not left!");

            Vector3 vEnu = robotBody.velocity;

            float vFluForward = Vector3.Dot(vEnu, enuForward);
            float vFluLeft = Vector3.Dot(vEnu, enuLeft);
            ActualVelocity = new Vector2(vFluForward, vFluLeft);

            float cmdForward = TargetVelocity.z;
            float cmdLeft = -TargetVelocity.x;
            if (Mathf.Abs(vFluForward) > MaxSpeed) cmdForward = 0f;
            if (Mathf.Abs(vFluLeft) > MaxSpeed) cmdLeft = 0f;

            float vModelForward = (float)discreteTfForward.Step(cmdForward);
            float vModelLeft = (float)discreteTfLateral.Step(cmdLeft);
            ModelVelocity = new Vector2(vModelForward, vModelLeft);

            float forceForward = DroneMass * (vModelForward - vFluForward) / Time.fixedDeltaTime;
            float forceLeft = DroneMass * (vModelLeft - vFluLeft) / Time.fixedDeltaTime;

            if (MaxForce > 0f)
            {
                forceForward = Mathf.Clamp(forceForward, -MaxForce, MaxForce);
                forceLeft = Mathf.Clamp(forceLeft, -MaxForce, MaxForce);
            }

            Vector3 forceForwardEnu = enuForward * forceForward;
            Vector3 forceLateralEnu = enuLeft * forceLeft;
            Vector3 totalHorizontalForce = forceForwardEnu + forceLateralEnu;
            Vector3 localHorizontalForce = robotBody.transform.InverseTransformDirection(totalHorizontalForce);
            return localHorizontalForce;
        }
    }
}