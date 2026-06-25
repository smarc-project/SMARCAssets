using Force;
using Smarc.GenericControllers;
using UnityEngine;
using VehicleComponents.Actuators;
using VehicleComponents.Sensors;


namespace dji
{

    public enum DroneFlightState
    {
        Idle,
        TakingOff,
        Flying,
        Landing
    }

    /// <summary>
    /// This controller bridges between the DJI interface and a simple force-based set of controllers (smarc/generic controllers)
    /// </summary>
    [RequireComponent(typeof(AltitudeController))]
    [RequireComponent(typeof(AttitudeController))]
    [RequireComponent(typeof(HorizontalController))]
    public class DJIController : MonoBehaviour
    {
        [Header("Settings")]
        public bool StartInAir = false;
        public bool GotControl = true;
        public DroneFlightState flightState = DroneFlightState.Idle;

        [Header("Payload")]
        public LoadCell WinchLoadCell;
        [Tooltip("If true, the alt. controller will be given this load as extra mass to compensate for.")]
        public bool CompensateForPayload = true; 


        [Header("Propellers(Upper)")]
        public Propeller frontLeftPropeller;
        public Propeller frontRightPropeller;
        public Propeller backLeftPropeller;
        public Propeller backRightPropeller;
        public float FloatRPM = 1000f;

        [Header("Propellers(Lower)")]
        [Tooltip("If true, will also control the downwards facing propellers")]
        public bool IsDualProp = false;
        public Propeller frontLeftDownPropeller;
        public Propeller frontRightDownPropeller;
        public Propeller backLeftDownPropeller;
        public Propeller backRightDownPropeller;

        float takeOffAltitude = 1.5f; // what the real thing does is 1.5m
        float homeAltitude; // altitude at which the drone took off

        AltitudeController altCtrl;
        AttitudeController attCtrl;
        HorizontalController horizCtrl;
        
        Vector3 commandedHorizontalVelocity = Vector3.zero;
        float lastHorizontalCommandTime = -1f;
        float commandedVerticalVelocity = 0f;
        float lastVerticalCommandTime = -1f;
        float commandedYawRate = 0f;
        float lastYawCommandTime = -1f;

        MixedBody robotBody;

        float stoppedFor = 0f;

        

        Matrix4x4 Binv;

        void Awake()
        {
            altCtrl = GetComponent<AltitudeController>();
            attCtrl = GetComponent<AttitudeController>();
            horizCtrl = GetComponent<HorizontalController>();
            robotBody = new MixedBody(altCtrl.RobotAB, altCtrl.RobotRB);

            homeAltitude = robotBody.position.y;
            
            altCtrl.ControlMode = AltitudeControlMode.AbsoluteAltitude;
            altCtrl.TargetVelocity = 0f;
            altCtrl.TargetAltitude = robotBody.position.y;

            attCtrl.YawControlMode = YawControlMode.YawRate;
            attCtrl.TargetYawRate = 0f;
            attCtrl.TiltMode = TiltMode.ReactToAcceleration;

            horizCtrl.ControlMode = HorizontalControlMode.UnityPosition;
            horizCtrl.TargetVelocity = Vector3.zero;
            horizCtrl.TargetUnityPosition = robotBody.position;

            float kf = 0.00012f; // thrust coefficient [N/rpm^2]
            float km = 0.000012f; // drag coefficient [Nm/rpm^2]
            float l  = 1.1f;  // arm length [m]
            float a = l / Mathf.Sqrt(2.0f);

            Binv = new Matrix4x4(
                new Vector4(kf,  a*kf, -a*kf,  km),
                new Vector4(kf, -a*kf, -a*kf, -km),
                new Vector4(kf, -a*kf,  a*kf,  km),
                new Vector4(kf,  a*kf,  a*kf, -km)
            ).inverse; // Control allocation for X-configuration drone.

            Ignition(StartInAir);
            if (StartInAir)
            {
                flightState = DroneFlightState.Flying;
                altCtrl.ControlMode = AltitudeControlMode.AbsoluteAltitude;
                altCtrl.TargetAltitude = homeAltitude;
                GotControl = true;
            }
        }


        void FixedUpdate()
        {
            RPMsFromMotion();

            if (CompensateForPayload && WinchLoadCell != null) altCtrl.ExtraMassToCompensate = WinchLoadCell.Weight;

            if (!GotControl) return;
            
            switch(flightState)
            {
                case DroneFlightState.TakingOff:
                    TakingOff();
                    break;
                case DroneFlightState.Landing:
                    Landing();
                    break;
                case DroneFlightState.Flying:
                    CommandHorizontal();
                    CommandVertical();
                    CommandYawRate();
                    break;
                case DroneFlightState.Idle:
                default:
                    // do nothing
                    break;
            }
        }

        void CommandHorizontal(float timeout=0.2f)
        {
            if (Time.time - lastHorizontalCommandTime > timeout)
            {
                commandedHorizontalVelocity = Vector3.zero;
                horizCtrl.ControlMode = HorizontalControlMode.UnityPosition;
                if (lastHorizontalCommandTime > 0)
                {
                    horizCtrl.TargetVelocity = Vector3.zero;
                    horizCtrl.TargetUnityPosition = robotBody.position;
                    lastHorizontalCommandTime = -1;
                }
            }
            else
            {
                horizCtrl.TargetVelocity = commandedHorizontalVelocity;
                horizCtrl.ControlMode = HorizontalControlMode.Velocity;
            }
        }

        void CommandVertical(float timeout=0.2f)
        {
            if (Time.time - lastVerticalCommandTime > timeout)
            {
                commandedVerticalVelocity = 0f; 
                altCtrl.ControlMode = AltitudeControlMode.AbsoluteAltitude;
                if (lastVerticalCommandTime > 0)
                {
                    altCtrl.TargetVelocity = 0f;
                    altCtrl.TargetAltitude = robotBody.position.y;
                    lastVerticalCommandTime = -1;
                }
            }
            else
            {
                altCtrl.TargetVelocity = commandedVerticalVelocity;
                altCtrl.ControlMode = AltitudeControlMode.VerticalVelocity;
            }
        }

        void CommandYawRate(float timeout=0.2f)
        {
            attCtrl.YawControlMode = YawControlMode.YawRate;
            if (Time.time - lastYawCommandTime > timeout) commandedYawRate = 0f;
            attCtrl.TargetYawRate = commandedYawRate;
        }

        void TakingOff()
        {
            altCtrl.ControlMode = AltitudeControlMode.AbsoluteAltitude;
            altCtrl.TargetAltitude = homeAltitude + takeOffAltitude;
            if (robotBody.position.y >= altCtrl.TargetAltitude - altCtrl.AltitudeTolerance)
            {
                flightState = DroneFlightState.Flying;
                Debug.Log("Takeoff complete, now flying");
            }
        }

        void Landing()
        {
            altCtrl.ControlMode = AltitudeControlMode.VerticalVelocity;
            altCtrl.TargetVelocity = -altCtrl.DescentRate;
            bool stopped = Mathf.Abs(robotBody.velocity.y) <= 0.2f;
            if (stopped) stoppedFor += Time.fixedDeltaTime;
            else stoppedFor = 0f;
            bool stuck = stoppedFor >= 1.0f; // if we've been stopped for 1 second, consider ourselves stuck
            if (stuck)
            {
                flightState = DroneFlightState.Idle;
                Debug.Log("Landing complete, now idle");
                Ignition(false);
            }
        }

        public bool TakeOff()
        {
            if (!GotControl) 
            {
                Debug.Log("Cannot take off, do not have control");
                return false;
            }

            if (flightState != DroneFlightState.Idle)
            {
                Debug.Log("Cannot take off, drone not idle");
                return false;
            }

            Debug.Log("DJI Taking off");
            flightState = DroneFlightState.TakingOff;
            Ignition(true);
            homeAltitude = robotBody.position.y;
            return true;
        }

        public bool Land()
        {
            if (!GotControl) 
            {
                Debug.Log("Cannot land, do not have control");
                return false;
            }

            if (flightState != DroneFlightState.Flying)
            {
                Debug.Log("Cannot land, drone not flying");
                return false;
            }

            Debug.Log("DJI Landing");
            flightState = DroneFlightState.Landing;
            stoppedFor = 0f;
            return true;
        }

        public bool TakeControl()
        {
            if (GotControl)
            {
                Debug.Log("Already have control");
                return true;
            }
            Debug.Log("DJI Taking control");
            GotControl = true;
            return true;
        }

        public bool ReleaseControl()
        {
            if (!GotControl)
            {
                Debug.Log("Do not have control to release");
                return false;
            }
            Debug.Log("DJI Releasing control");
            GotControl = false;
            return true;
        }

        public bool TurnOnMotors()
        {
            Ignition(true);
            return true;
        }

        public bool TurnOffMotors()
        {
            Ignition(false);
            return true;
        }

        void Ignition(bool on)
        {
            if (!GotControl)
            {
                Debug.Log("Cannot change ignition state, do not have control");
                return;
            }

            frontLeftPropeller.SetRpm(on? FloatRPM : 0f);
            frontRightPropeller.SetRpm(on? FloatRPM : 0f);
            backLeftPropeller.SetRpm(on? FloatRPM : 0f);
            backRightPropeller.SetRpm(on? FloatRPM : 0f);
            altCtrl.enabled = on;
            altCtrl.CompensateGravity = on;
            attCtrl.enabled = on;
            horizCtrl.enabled = on;

            if (IsDualProp)
            {
                frontLeftDownPropeller.SetRpm(frontLeftPropeller.rpm);
                frontRightDownPropeller.SetRpm(frontRightPropeller.rpm);
                backLeftDownPropeller.SetRpm(backLeftPropeller.rpm);
                backRightDownPropeller.SetRpm(backRightPropeller.rpm);
            }
        }

        void RPMsFromMotion()
        {   
            Vector3 torque = attCtrl.LastAppliedTorque; 
            float thrust = attCtrl.LastAppliedThrust;
            Vector4 wrench = new Vector4(thrust, torque.x, torque.y, torque.z);
            Vector4 rpmSquared = Binv * wrench;

            if (IsDualProp)
            {
                rpmSquared /= 2f; // if we have dual props, each prop only needs to provide half the thrust/torque, so we can divide the required wrench by 2 before converting to RPM^2
            }

            frontLeftPropeller.SetRpm(Mathf.Sqrt(Mathf.Max(0f, rpmSquared.x)));
            frontRightPropeller.SetRpm(Mathf.Sqrt(Mathf.Max(0f, rpmSquared.y)));
            backLeftPropeller.SetRpm(Mathf.Sqrt(Mathf.Max(0f, rpmSquared.z)));
            backRightPropeller.SetRpm(Mathf.Sqrt(Mathf.Max(0f, rpmSquared.w)));

            if (IsDualProp)
            {
                frontLeftDownPropeller.SetRpm(frontLeftPropeller.rpm);
                frontRightDownPropeller.SetRpm(frontRightPropeller.rpm);
                backLeftDownPropeller.SetRpm(backLeftPropeller.rpm);
                backRightDownPropeller.SetRpm(backRightPropeller.rpm);
            }
        }

        public void CommandFLUYawRate(float forward, float left, float up, float yawRate)
        {
            if (!GotControl)
            {
                Debug.Log("Cannot command drone, do not have control");
                return;
            }
            // Unity is RUF, do the mapping here.
            if (forward != 0f || left != 0f)
            {
                commandedHorizontalVelocity = new Vector3(-left, 0f, forward);
                lastHorizontalCommandTime = Time.time;
            }
            if (up != 0f)
            {
                commandedVerticalVelocity = up;
                lastVerticalCommandTime = Time.time;
            }
            if (yawRate != 0f)
            {
                commandedYawRate = -yawRate;
                lastYawCommandTime = Time.time;
            }
        }

        public void CommandFLUYawRate01(float forward, float left, float up, float yawRate)
        {
            CommandFLUYawRate(forward * horizCtrl.MaxSpeed, left * horizCtrl.MaxSpeed, up * altCtrl.AscentRate, yawRate * attCtrl.DesiredYawRate);
        }
    }
}