using Smarc.GenericControllers;
using UnityEngine;
using VehicleComponents.Actuators;

namespace Evolo
{
    [RequireComponent(typeof(AltitudeControllerPID))]
    [RequireComponent(typeof(AttitudeControllerPID))]
    [RequireComponent(typeof(HorizontalController))]
    public class EvoloController : MonoBehaviour, IGenericTwistActuator
    {
        [Header("Targets")]
        public float Speed;
        public float YawRate;
        public float Altitude = 1f;
        
        [Header("Limits")]
        public float MaxYawRate = 5f;
        public float MaxSpeed = 7f;
        public float MaxAltitude = 2f;

        AltitudeControllerPID altCtrl;
        AttitudeControllerPID attCtrl;
        HorizontalController horizCtrl;

        Vector3 twistLinear = Vector3.zero;
        Vector3 twistAngular = Vector3.zero;

        

        void Awake()
        {
            altCtrl = GetComponent<AltitudeControllerPID>();
            altCtrl.ControlMode = AltitudeControlMode.AltitudeFromWater;
            altCtrl.TargetAltitude = Altitude;

            attCtrl = GetComponent<AttitudeControllerPID>();
            attCtrl.YawControlMode = YawControlMode.YawRate;
            attCtrl.TargetYawRate = 0f;
            attCtrl.TiltMode = TiltMode.ReactToAcceleration;

            horizCtrl = GetComponent<HorizontalController>();
            horizCtrl.ControlMode = HorizontalControlMode.Velocity;
            horizCtrl.TargetVelocity = Vector3.zero;
        }

        void FixedUpdate()
        {
            Speed = Mathf.Clamp(Speed, -MaxSpeed, MaxSpeed);
            horizCtrl.TargetVelocity = new Vector3(0, 0, Mathf.Clamp(Speed, -MaxSpeed, MaxSpeed));
            twistLinear.z = Speed;
            YawRate = Mathf.Clamp(YawRate, -MaxYawRate, MaxYawRate);
            twistAngular.y = YawRate;
            attCtrl.TargetYawRate = Mathf.Clamp(YawRate, -MaxYawRate, MaxYawRate);
            Altitude = Mathf.Clamp(Altitude, 0, MaxAltitude);
            altCtrl.TargetAltitude = Altitude;
        }

        public (Vector3, Vector3) GetCurrentValue()
        {
            return (twistLinear, twistAngular);
        }

        public (Vector3, Vector3) GetResetValue()
        {
            return (Vector3.zero, Vector3.zero);
        }

        public bool HasNewData()
        {
            return true;
        }

        public void SetTwist(Vector3 LinearVelocity, Vector3 AngularVelocity)
        {
            Speed = LinearVelocity.z;
            YawRate = AngularVelocity.y;
        }
    }
}