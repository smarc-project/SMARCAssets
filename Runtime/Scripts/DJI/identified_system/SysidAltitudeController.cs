using UnityEngine;
using Smarc.GenericControllers;

namespace dji
{
    // NOTE: the identified model below (b_Gzd / a_Gzd) was fit from flight data WITHOUT any payload
    // attached. If ExtraMassToCompensate (or any other payload) is used, the extra mass is only
    // accounted for in the gravity compensation term - the model itself has no knowledge of it, so
    // the simulated dynamics will likely diverge noticeably from the real drone's behaviour in that case.
    [AddComponentMenu("Smarc/DJI/Sysid Altitude Controller")]
    public class SysidAltitudeController : AltitudeControllerBase
    {

        [Header("Identified model — descending powers of z, T = 0.02 s")]
        [Tooltip("Identified WITHOUT a payload attached - see class-level note.")]
        [SerializeField] double[] b_Gzd = { 0.06029777678231629 };
        [SerializeField] double[] a_Gzd = { 1.0, -0.9375490118180505 };
        public float ModelVelocity  { get; private set; }
        public float ActualVelocity { get; private set; }
        DiscreteTf discreteTf;


        new void Start()
        {
            base.Start();
            discreteTf = new DiscreteTf(b_Gzd, a_Gzd);
        }

        protected override Vector3 VelocityControl()
        {
            ActualVelocity = robotBody.velocity.y;

            float cmd = TargetVelocity;
            if (ActualVelocity > AscentRate) cmd = 0f;
            if (ActualVelocity < -DescentRate) cmd = 0f;

            float vModelZ = (float)discreteTf.Step(cmd);
            ModelVelocity = vModelZ;

            float forceZ = totalMass * (ModelVelocity - ActualVelocity) / Time.fixedDeltaTime;
            if (CompensateGravity) forceZ += totalMass * -Physics.gravity.y;
            forceZ += ExtraMassToCompensate * -Physics.gravity.y;
            if (MaxForce > 0f) forceZ = Mathf.Clamp(forceZ, -MaxForce, MaxForce);

            Vector3 forceEnu = Vector3.up * forceZ;
            Debug.DrawRay(COM.position, forceEnu * 0.1f, Color.red);
            return forceEnu;
        }

    }
}
