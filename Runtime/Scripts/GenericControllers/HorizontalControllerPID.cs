using UnityEngine;
using Force;


namespace Smarc.GenericControllers
{

    [AddComponentMenu("Smarc/Generic Controllers/Horizontal Controller PID")]
    public class HorizontalController : HorizontalControllerBase
    {
        
        [Header("Velocity PID")]
        public float VelKp = 5.0f;
        public float VelKi = 0.0f;
        public float VelKd = 0.0f;
        public float VelIntegratorLimit = 5f;
        PID velPID;
        

        new void Start()
        {
            base.Start();
            velPID = new PID(VelKp, VelKi, VelKd, VelIntegratorLimit, maxOutput:MaxForce);
        }

        protected override Vector3 GetHorizontalForceLocal()
        {
            if (ControlMode == HorizontalControlMode.UnityPosition)
            {
                Vector3 diff = TargetUnityPosition - COM.position;
                if (diff.magnitude <= PositionTolerance) TargetVelocity = Vector3.zero;
                else TargetVelocity = diff.normalized * MaxSpeed;
                Debug.DrawLine(COM.position, TargetUnityPosition, Color.green);
            }
            
            TargetVelocity.y = 0;

            Vector3 currentVelocity = robotBody.localVelocity;
            currentVelocity.y = 0;
            if(TargetVelocity.magnitude > MaxSpeed)
            {
                TargetVelocity = TargetVelocity.normalized * MaxSpeed;
            }

            Vector3 force = velPID.UpdateVector3(TargetVelocity, currentVelocity, Time.fixedDeltaTime);
            return force;
        }

    }
}
