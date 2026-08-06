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
    [RequireComponent(typeof(AltitudeControllerBase))]
    [RequireComponent(typeof(AttitudeControllerBase))]
    [RequireComponent(typeof(HorizontalControllerBase))]
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

        AltitudeControllerBase altCtrl;
        AttitudeControllerBase attCtrl;
        HorizontalControllerBase horizCtrl;
        
        Vector3 commandedHorizontalVelocity = Vector3.zero;
        float lastHorizontalCommandTime = -1f;
        float commandedVerticalVelocity = 0f;
        float lastVerticalCommandTime = -1f;
        float commandedYawRate = 0f;
        float lastYawCommandTime = -1f;

        MixedBody robotBody;

        float stoppedFor = 0f;

        void Awake()
        {
            // there might be many, make sure only one is enabled!
            AltitudeControllerBase[] altCtrls = GetComponents<AltitudeControllerBase>();
            altCtrl = null;
            foreach (var ctrl in altCtrls)
            {
                if (ctrl.enabled)
                {
                    if (altCtrl == null) altCtrl = ctrl;
                    else
                    {
                        Debug.LogWarning($"Multiple AltitudeControllerBase components found on {gameObject.name}, disabling all but one");
                        ctrl.enabled = false;
                    }
                }
            }
            if (altCtrl == null)
            {
                Debug.LogError($"No enabled AltitudeControllerBase component found on {gameObject.name}, disabling DJIController");
                enabled = false;
                return;
            }

            AttitudeControllerBase[] attCtrls = GetComponents<AttitudeControllerBase>();
            attCtrl = null;
            foreach (var ctrl in attCtrls)
            {
                if (ctrl.enabled)
                {
                    if (attCtrl == null) attCtrl = ctrl;
                    else
                    {
                        Debug.LogWarning($"Multiple AttitudeControllerBase components found on {gameObject.name}, disabling all but one");
                        ctrl.enabled = false;
                    }
                }
            }
            if (attCtrl == null)
            {
                Debug.LogError($"No enabled AttitudeControllerBase component found on {gameObject.name}, disabling DJIController");
                enabled = false;
                return;
            }

            HorizontalControllerBase[] horizCtrls = GetComponents<HorizontalControllerBase>();
            horizCtrl = null;
            foreach (var ctrl in horizCtrls)
            {
                if (ctrl.enabled)
                {
                    if (horizCtrl == null) horizCtrl = ctrl;
                    else
                    {
                        Debug.LogWarning($"Multiple HorizontalControllerBase components found on {gameObject.name}, disabling all but one");
                        ctrl.enabled = false;
                    }
                }
            }
            if (horizCtrl == null)
            {
                Debug.LogError($"No enabled HorizontalControllerBase component found on {gameObject.name}, disabling DJIController");
                enabled = false;
                return;
            }

            
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
            float tiltAngle = Vector3.Angle(robotBody.transform.up, Vector3.up);

            // more tilt = higher RPM needed for all the props
            float tiltFactor = 1f + (tiltAngle / 90f) * 0.5f; // scales from 1.0 to 1.5

            // + if the hub of a prop is moving up or down, it needs more or less RPM
            float maxSpeed = 5f;

            float propSpeedFactor(Propeller p)
            {
                MixedBody body = p.GetMixedBody();
                float verticalVelocity = Vector3.Dot(body.velocity, Vector3.up);
                return 1f + (verticalVelocity / maxSpeed);
            }
            
            float idle_mult = flightState == DroneFlightState.Idle ? 0f : 1f;
            float landing_mult = flightState == DroneFlightState.Landing ? 0.5f : 1f;
            float takingoff_mult = flightState == DroneFlightState.TakingOff ? 1.5f : 1f;
            float state_mult = idle_mult * landing_mult * takingoff_mult;

            float flSpeedFactor = propSpeedFactor(frontLeftPropeller);
            float frSpeedFactor = propSpeedFactor(frontRightPropeller);
            float blSpeedFactor = propSpeedFactor(backLeftPropeller);
            float brSpeedFactor = propSpeedFactor(backRightPropeller);


            frontLeftPropeller.SetRpm(FloatRPM * tiltFactor * flSpeedFactor * state_mult);
            frontRightPropeller.SetRpm(FloatRPM * tiltFactor * frSpeedFactor * state_mult);
            backLeftPropeller.SetRpm(FloatRPM * tiltFactor * blSpeedFactor * state_mult);
            backRightPropeller.SetRpm(FloatRPM * tiltFactor * brSpeedFactor * state_mult);

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
            CommandFLUYawRate(forward * horizCtrl.MaxSpeed, left * horizCtrl.MaxSpeed, up * altCtrl.AscentRate, yawRate * attCtrl.MaxYawRateDeg);
        }
    }
}