using UnityEngine;


namespace Smarc.GenericControllers
{

    [AddComponentMenu("Smarc/Generic Controllers/Altitude Controller")]
    public class AltitudeControllerPID : AltitudeControllerBase
    {
        [Header("Velocity PID")]
        public float VelKp = 5.0f;
        public float VelKi = 0.0f;
        public float VelKd = 0.0f;
        public float VelIntegratorLimit = 5f; // limits integral term (in meter-seconds)
        PID velPID;

        
        new void Start()
        {
            base.Start();
            velPID = new PID(VelKp, VelKi, VelKd, VelIntegratorLimit, maxOutput:MaxForce);
        }
     

        protected override Vector3 VelocityControl()
        {
            float currentVel = robotBody.velocity.y;
            float pidAcc = velPID.Update(TargetVelocity, currentVel, Time.fixedDeltaTime);
            pidAcc = LimitAccelation(pidAcc, currentVel, Time.fixedDeltaTime);

            float requiredForce = pidAcc;
            if (CompensateGravity) requiredForce += totalMass * -Physics.gravity.y;
            requiredForce += ExtraMassToCompensate * -Physics.gravity.y;
            requiredForce = MaxForce > 0f ? Mathf.Clamp(requiredForce, -MaxForce, MaxForce) : requiredForce;

            Vector3 upForce = Vector3.up * requiredForce;
            Debug.DrawRay(COM.position, upForce * 0.1f, Color.red);
            return upForce;
        }
        

        
    }
}