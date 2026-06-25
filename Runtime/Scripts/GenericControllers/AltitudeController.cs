using UnityEngine;
using Force;
using DefaultNamespace.Water;


namespace Smarc.GenericControllers
{
    public enum AltitudeControlMode
    {
        AbsoluteAltitude,
        VerticalVelocity,
        AltitudeFromWater
    }

    [AddComponentMenu("Smarc/Generic Controllers/Altitude Controller")]
    public class AltitudeController : MonoBehaviour
    {
        public ArticulationBody RobotAB;
        public Rigidbody RobotRB;
        private MixedBody robotBody;

        public AltitudeControlMode ControlMode = AltitudeControlMode.AbsoluteAltitude;
        [Tooltip("If true, the controller will only apply altitude control when the robot is moving forward.")]
        public bool OnlyIfMovingForward = false;
        [Tooltip("If true, gravity compensation will be applied before control.")]
        public bool CompensateGravity = true;
        [Min(0), Tooltip("When there is a payload attached, without doing fancy controls.")]
        public float ExtraMassToCompensate = 0f;
        [Tooltip("If true, the COM calculations will include all child rigidbodies/articulation bodies. If your robot is very complex, the controller might behave funny.")]
        public bool IncludeChildrenInCom = false;
        [Tooltip("If true, the mass of all children will be negated before control is applied.")]
        public bool IncludeChildrenInGravityComp = false;


        public float AscentRate = 2.0f;
        public float DescentRate = 2.0f;
        [Tooltip("Set to 0 to disable force capping")]
        public float MaxForce = 0f;


        [Header("Velocity Settings")]
        public float TargetVelocity = 0.0f;
        [Tooltip("Depending on the vehicle, very low targets can lead to PID control being dumb. Set to 0 to disable.")]
        public float MinimumDescentTargetVelocity = 0f;
        [Tooltip("Depending on the vehicle, very low targets can lead to PID control being dumb. Set to 0 to disable.")]
        public float MinimumAscentTargetVelocity = 0f;


        [Header("Position Settings")]
        public float TargetAltitude = 10.0f;
        public float AltitudeTolerance = 0.1f;
        public float GroundLevel = 0f; 


        [Header("Velocity PID")]
        public float VelKp = 5.0f;
        public float VelKi = 0.0f;
        public float VelKd = 0.0f;
        public float VelIntegratorLimit = 5f; // limits integral term (in meter-seconds)
        PID velPID;
        

        public Vector3 LastAppliedForce { get; private set; }

        
        // Use a generated object to apply force at center of mass, parented to the robot transform
        // This way, we don't have to recalculate the world position of the COM every frame
        Transform COM;
        float totalMass;

        WaterQueryModel waterModel;


        void Start()
        {
            robotBody = new MixedBody(RobotAB, RobotRB);
            velPID = new PID(VelKp, VelKi, VelKd, VelIntegratorLimit, maxOutput: MaxForce);
            totalMass = robotBody.GetTotalConnectedMass(includeChildren:IncludeChildrenInGravityComp);
            var globalCom = robotBody.GetTotalConnectedCenterOfMass(includeChildren:IncludeChildrenInCom);
            COM = new GameObject("AltitudeController_COM").transform;
            COM.parent = robotBody.transform;
            COM.position = globalCom;
        }


        void FixedUpdate()
        {
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

        float LimitAccelation(float desiredAcc, float currentVel, float deltaTime)
        {
            // limit acceleration so we don't instantly exceed configured ascent/descent rates
            float maxAccThisStepUp = (AscentRate - currentVel) / deltaTime;
            float maxAccThisStepDown = (-DescentRate - currentVel) / deltaTime;
            float minAcc = Mathf.Min(maxAccThisStepDown, maxAccThisStepUp);
            float maxAcc = Mathf.Max(maxAccThisStepDown, maxAccThisStepUp);
            return Mathf.Clamp(desiredAcc, minAcc, maxAcc);
        }


        void VelocityControl()
        {
            float currentVel = robotBody.velocity.y;
            float pidAcc = velPID.Update(TargetVelocity, currentVel, Time.fixedDeltaTime);
            pidAcc = LimitAccelation(pidAcc, currentVel, Time.fixedDeltaTime);

            float requiredForce = pidAcc;
            if (CompensateGravity) requiredForce += totalMass * -Physics.gravity.y;
            requiredForce += ExtraMassToCompensate * -Physics.gravity.y;
            requiredForce = MaxForce > 0f ? Mathf.Clamp(requiredForce, -MaxForce, MaxForce) : requiredForce;

            Vector3 upForce = Vector3.up * requiredForce;
            robotBody.AddForceAtPosition(upForce, COM.position, ForceMode.Force);
            LastAppliedForce = upForce;
            Debug.DrawRay(COM.position, upForce * 0.1f, Color.red);
        }
        

        void OnDrawGizmosSelected()
        {
            // Draw target altitude line
            Gizmos.color = Color.purple;
            Transform tf = this.transform;
            if(RobotAB != null)
                tf = RobotAB.transform;
            else if(RobotRB != null)
                tf = RobotRB.transform;
            Vector3 startPos = tf.position;
            Vector3 endPos = new(tf.position.x+0.1f, GroundLevel + TargetAltitude, tf.position.z+0.1f);
            Gizmos.DrawLine(startPos, endPos);
        }
    }
}