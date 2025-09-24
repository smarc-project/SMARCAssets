using UnityEngine;
using VehicleComponents.Actuators;
using System;
using System.IO;
using System.Globalization;
using M350.PSDK_ROS2;
using VehicleComponents.Sensors;
using UnityEditor.EditorTools;


namespace dji
{
    
    public enum ControllerType
    {
        FLU_Velocity,
        ENU_RelativePosition, 
        FLU_Attitude,
    }

    public class DJIController : MonoBehaviour
    {
        [Header("Drone Components")]
        public ArticulationBody ComAB;
        public Propeller FL, FR, BL, BR;
        [Tooltip("Maximum RPM the props can reach. This overrides the max RPM set in the Propeller script")]
        public float MaxRPM = 8000f;


        [Header("Control Commands")]
        [Tooltip("Set to true when landed to cut power to motors")]
        public bool MotorsOff = true;
        public ControllerType ControllerType = ControllerType.FLU_Velocity;
        public Vector3 CommandVelocityFLU = Vector3.zero;
        public Vector3 CommandPositionENU = Vector3.zero;
        [Tooltip("target Pitch when in Attitude control mode")]
        public float CommandPitch = 0;
        [Tooltip("target Roll when in Attitude control mode")]
        public float CommandRoll = 0;
        [Tooltip("target Yaw, regardless of control mode")]
        public float CommandYaw = 0;
        [Tooltip("Height to hover at when in Attitude control mode")]
        public float TargetAlt;
        [Tooltip("The 'base' speed the props rotate at. Should be slightly below the critical speed that produces enough thrust to take off")]
        public float BaseRPM = 3000;

        [Header("Takeoff and Landing")]
        public float TakeOffAltitude = 5;
        public float TakeOffError = .1f;
        public float LandingError = .05f;


        [Header("Data Logging")]
        //Path to which flight data is recorded. Should be a .csv
        public bool log_data = false;
        public string data_path = @"c:\School\UnityLogger.csv";

        [Header("Controller Limits")]
        public float MaxControllerOutputAltitude = 2000;
        public float MaxControllerOutputPitchRoll = 1000;
        public float MaxENUSpeed = .8f;

        public bool IsTakingOff { get; private set; } = false;
        public bool IsLanding { get; private set; } = false;



        float comPitch, comRoll;
        float comYaw;


        public Vector3 Position { get; private set;}
        Quaternion worldRotation;
        Vector3 comVel;
        Vector3 localVel;
        Vector3 FLUVel;

        float altitude;
        float target_pitch;
        float target_roll;
        float target_yaw;

        PsdkTakeoffService takeoff_srv = null;
        PsdkLandingService landing_srv = null;
        LockedDirectionDepthSensor depthSensor = null;


        //Used for altitude controller
        float prev_alt_error_pos = 0;
        float prev_alt_output_pos = 0;
        float alt_error_pos;

        //Used for vertical speed controller
        float prev_alt_error_vel = 0;
        float prev_alt_output_vel = 0;
        float alt_error_vel;

        //Used for pitch controller
        float prev_pitch_error = 0;
        float prev_pitch_output = 0;
        float pitch_error;

        //Used for roll controller
        float prev_roll_error = 0;
        float prev_roll_output = 0;
        float roll_error;

        //Used for yaw controller
        float prev_yaw_error = 0;
        float prev_yaw_output = 0;
        float yaw_error;
        float yaw_output;

        //Metrics for forward speed controller 
        [Header("Velocity Controller Tuning")]
        [Tooltip("Larger -> Faster")]
        [Range(0,100)]
        public float K_VelForw = 30f;
        [Tooltip("Larger -> More Stable")]
        [Range(0,1)]
        public float Z_VelForw = .9f;
        [Tooltip("Larger -> More Aggressive, more overshoot")]
        [Range(0,1)]
        public float P_VelForw = .8f;

        //Metrics for side-to-side speed controller
        [Tooltip("Larger -> Faster")]
        [Range(0,100)]
        public float K_VelLeft = 20f;
        [Tooltip("Larger -> More Stable")]
        [Range(0,1)]
        public float Z_VelLeft = 0f;
        [Tooltip("Larger -> More Aggressive, more overshoot")]
        [Range(0,1)]
        public float P_VelLeft = 0f;

        //Used for speed controller
        Vector3 prev_vel_error = Vector3.zero;
        Vector3 prev_vel_output = Vector3.zero;
        Vector3 vel_error = Vector3.zero;

        //Used for position controller
        Vector3 prev_ENU_pos_output = Vector3.zero;
        Vector3 ENU_pos_output = Vector3.zero;

        //Used to reduce the frequency of the speed controller for better performance
        float vel_counter = 0;
        float vel_counter_max = 25;
        float pos_counter = 0;
        float pos_counter_max = 50; //.1 second period



        void OnValidate(){
            float delta = Time.fixedDeltaTime - 0.002f;
            if (Mathf.Abs(delta) > .0001)
            {
                if (gameObject.scene.IsValid() && enabled) Debug.LogError("Disabling DJI Controller. Set fixed time step to .002 s (Edit->Project Settings->Time->Fixed Timestep) for DJI captain to be functional. Currently: " + Time.fixedDeltaTime);
                enabled = false;
            }
        }

        void Awake()
        {
            FL.HoverDefault = false;
            BL.HoverDefault = false;
            FR.HoverDefault = false;
            BR.HoverDefault = false;
            FL.RPMMax = MaxRPM;
            BL.RPMMax = MaxRPM;
            FR.RPMMax = MaxRPM;
            BR.RPMMax = MaxRPM;
        }

        void Start(){
            if(!enabled){
                return;
            }
            if(takeoff_srv == null){
                takeoff_srv = GetComponentInChildren<PsdkTakeoffService>();
            }
            if(landing_srv == null){
                landing_srv = GetComponentInChildren<PsdkLandingService>();
            }
            if(depthSensor == null){
                depthSensor = GetComponentInChildren<LockedDirectionDepthSensor>();
            }
            if(log_data){
                DateTime localDate = DateTime.Now;
                DateTime utcDate = DateTime.UtcNow;
                if (!File.Exists(data_path))
                {
                    // Create a file to write to.
                    using (StreamWriter sw = File.CreateText(data_path))
                    {
                        sw.WriteLine("NEW, {0}", localDate.ToString(new CultureInfo("en-GB")));
                        sw.WriteLine("Time, Roll, Pitch, Yaw, Target Roll, Target Pitch, Target Yaw, VelForw, VelLeft, VelUp, Target VelForw, Target VelLeft, Target VelUp, FL, FR, BL, BR, Target Pitch, Target Roll");
                    }
                }
                else{
                    using (StreamWriter sw = File.AppendText(data_path)){
                        sw.WriteLine("NEW, {0}", localDate.ToString(new CultureInfo("en-GB")));
                        sw.WriteLine("Time, Roll, Pitch, Yaw, Target Roll, Target Pitch, Target Yaw, VelForw, VelLeft, VelUp, Target VelForw, Target VelLeft, Target VelUp, FL, FR, BL, BR, Target Pitch, Target Roll");
                    }
                }
            }
        }

        public bool TakeOff()
        {
            if(ControllerType != ControllerType.FLU_Velocity || Position.y >= TakeOffAltitude - TakeOffError){
                Debug.Log("Can't take off. Either not in velocity control mode or already above takeoff altitude");
                return false;
            }
            Debug.Log("Taking off");
            IsTakingOff = true;
            IsLanding = false;
            MotorsOff = false;
            TargetAlt = TakeOffAltitude;
            return true;
        }

        public bool Land()
        {
            if(ControllerType != ControllerType.FLU_Velocity){
                Debug.Log("Can't land. Not in velocity control mode");
                return false;
            }
            IsTakingOff = false;
            IsLanding = true;
            TargetAlt = Position.y - depthSensor.depth;
            return true;
        }
 
        void FixedUpdate()
        {
            if(!enabled){
                return;
            }
            
            //Current rotation about the center of mass
            comPitch = ComAB.transform.rotation.eulerAngles.z;
            comRoll = ComAB.transform.rotation.eulerAngles.x;
            comYaw = ComAB.transform.rotation.eulerAngles.y;

            worldRotation = ComAB.transform.rotation;
            comVel = ComAB.linearVelocity;
            localVel = Quaternion.Inverse(worldRotation) * comVel;
            FLUVel.y = localVel.z;
            FLUVel.x = localVel.x;
            FLUVel.z = localVel.y;
            Position = ComAB.transform.position;
            altitude = Position.y;


            if(MotorsOff){
                FL.SetRpm(0);
                FR.SetRpm(0);
                BL.SetRpm(0);
                BR.SetRpm(0);
                return;
            }
            vel_counter++;
            pos_counter++;

            if(takeoff_srv == null){
                takeoff_srv = GetComponentInChildren<PsdkTakeoffService>();
            }
            if(landing_srv == null){
                landing_srv = GetComponentInChildren<PsdkLandingService>();
            }
            if(depthSensor == null){
                depthSensor = GetComponentInChildren<LockedDirectionDepthSensor>();
            }

            target_yaw = CommandYaw; //target and command angles are split so that the same attitude control can be used for velocity. For yaw, these are currently always equivalent.

            if(ControllerType == ControllerType.ENU_RelativePosition){
                TargetAlt = CommandPositionENU.z;
            }
            //Vertical Controllers
            float alt_output;
            if(ControllerType == ControllerType.FLU_Attitude || ControllerType == ControllerType.ENU_RelativePosition || IsTakingOff || IsLanding){
                //Altitude Controller. This is used either in Attitude Control Mode or while taking off or landing.
                alt_error_pos = TargetAlt - altitude;

                float k_alt_pos = 2712f; 
                float z_alt_pos = .999263f;
                float p_alt_pos = .9937f;

                alt_output = k_alt_pos * alt_error_pos - k_alt_pos * z_alt_pos * prev_alt_error_pos + p_alt_pos * prev_alt_output_pos; //Using a lead/lag controller with the defined gain, k, zero, z, and pole, p.
                if(alt_output > MaxControllerOutputAltitude){
                    alt_output = MaxControllerOutputAltitude;
                }
                else if(alt_output < -100){
                    alt_output = -100;
                }
                prev_alt_error_pos = alt_error_pos;
                prev_alt_output_pos = alt_output;

                if(IsTakingOff){ //Checks if done taking off
                    if(altitude > TakeOffAltitude - TakeOffError){
                        IsTakingOff = false;
                        Debug.Log("Setting takeoff to false");
                    }
                }

                if(IsLanding){
                    Debug.Log("Depth: " + depthSensor.depth + " landingError: " + LandingError);
                    if(depthSensor.depth > LandingError){
                        TargetAlt = altitude - depthSensor.depth;
                    }
                    else{
                        IsLanding = false;
                        Debug.Log("Setting landing to false");
                        if(!depthSensor.usingWater){
                            MotorsOff = true;
                        }
                        
                    }
                }
            }

            else{
                //Vertical Speed controller 
                alt_error_vel = CommandVelocityFLU.z - FLUVel.z;

                float k_alt_vel = 3000f;
                float z_alt_vel = .85f;
                float p_alt_vel = .9f;

                alt_output = k_alt_vel * alt_error_vel - k_alt_vel * z_alt_vel * prev_alt_error_vel + p_alt_vel * prev_alt_output_vel; //Using a lead/lag controller with the defined gain, k, zero, z, and pole, p.
                if(alt_output > MaxControllerOutputAltitude){
                    alt_output = MaxControllerOutputAltitude;
                }
                else if(alt_output < -MaxControllerOutputAltitude){
                    alt_output = -MaxControllerOutputAltitude;
                }
                prev_alt_error_vel = alt_error_vel;
                prev_alt_output_vel = alt_output;
            }

            if(ControllerType != ControllerType.ENU_RelativePosition){
                prev_ENU_pos_output = FLUVel;
            }

            if(ControllerType == ControllerType.FLU_Velocity){
                vel_error = FLUVel - CommandVelocityFLU;
            }
            else if(pos_counter >= pos_counter_max && ControllerType == ControllerType.ENU_RelativePosition){
                //Main Position Controller Code. This is not currently based on drone dynamics and instead mimics captain.
                pos_counter = 0;
                Vector3 pos_in_FLU = Vector3.zero;
                var yaw_rad = comYaw / 180 * Mathf.PI;
                pos_in_FLU.x = Mathf.Cos(yaw_rad) * CommandPositionENU.x - Mathf.Sin(yaw_rad) * CommandPositionENU.y;
                pos_in_FLU.y = Mathf.Cos(yaw_rad) * CommandPositionENU.y + Mathf.Sin(yaw_rad) * CommandPositionENU.x;

                float k_pos = .5f;
                float r_sigma = .9f;

                ENU_pos_output = pos_in_FLU * k_pos;

                ENU_pos_output = _normalize_max_speed(ENU_pos_output);

                ENU_pos_output = ENU_pos_output * (1 - r_sigma) + prev_ENU_pos_output * r_sigma;
                ENU_pos_output = _normalize_max_speed(ENU_pos_output);

                vel_error = FLUVel - ENU_pos_output;
                prev_ENU_pos_output = ENU_pos_output;
            }

            if(ControllerType == ControllerType.FLU_Attitude){
                target_pitch = CommandPitch;
                target_roll = CommandRoll;
            }

            else if(vel_counter >= vel_counter_max){
                //if statement used to reduce the frequency of the velocity controller. This is done to improve the performance by making it slower than the attitude controller.
                vel_counter = 0;
                target_pitch = vel_error.x * K_VelForw - prev_vel_error.x * K_VelForw * Z_VelForw + prev_vel_output.x * P_VelForw; //Using a lead/lag controller with the defined gain, k, zero, z, and pole, p.
                target_roll = -(vel_error.y * K_VelLeft - prev_vel_error.y * K_VelLeft * Z_VelLeft + prev_vel_output.y * P_VelLeft); //Using a lead/lag controller with the defined gain, k, zero, z, and pole, p.
                prev_vel_error = vel_error;
                
                //Roll and pitch limited to 20 degrees. This is partly for safety and partly because the controller was designed around a linear approximation of sine.
                if(target_pitch > 20){
                    target_pitch = 20;
                }
                else if(target_pitch < -20){
                    target_pitch = -20;
                }
                
                if(target_roll > 20){
                    target_roll = 20;
                }
                else if(target_roll < -20){
                    target_roll = -20;
                }

                prev_vel_output.x = target_pitch;
                prev_vel_output.y = target_roll;

            }

            //Pitch controller
            pitch_error = target_pitch - comPitch;
            if (pitch_error > 180f) pitch_error -= 360f;
            if (pitch_error < -180f) pitch_error += 360f;

            float k = 1200f; //Larger makes faster
            float z = 0.98f; //Larger makes mores stable (between 0 and 1)
            float p = .95f; //Larger makes "more aggressive", faster with more overshoot (between 0 and 1)

            var pitch_output = k * pitch_error - k * z * prev_pitch_error + p * prev_pitch_output; //Using a lead/lag controller with the defined gain, k, zero, z, and pole, p.
            if(pitch_output > MaxControllerOutputPitchRoll){
                pitch_output = MaxControllerOutputPitchRoll; //This is in prop rpm. Limiting to avoid physical and sim max speeds 
            }
            else if(pitch_output < -MaxControllerOutputPitchRoll){
                pitch_output = -MaxControllerOutputPitchRoll;
            } 
            prev_pitch_error = pitch_error;
            prev_pitch_output = pitch_output;

            roll_error = target_roll - comRoll;
            if (roll_error > 180f) roll_error -= 360f;
            if (roll_error < -180f) roll_error += 360f;

            //Roll Controller
            k = 1200f;
            z = .98f;
            p = .95f;

            var roll_output = k * roll_error - k * z * prev_roll_error + p * prev_roll_output; //Using a lead/lag controller with the defined gain, k, zero, z, and pole, p.
            if(roll_output > MaxControllerOutputPitchRoll){
                roll_output = MaxControllerOutputPitchRoll;
            }
            else if(roll_output < -MaxControllerOutputPitchRoll){
                roll_output = -MaxControllerOutputPitchRoll;
            }
            prev_roll_error = roll_error;
            prev_roll_output = roll_output;

            //Yaw Controller
            yaw_error = target_yaw - comYaw;
            if (yaw_error > 180f) yaw_error -= 360f;
            if (yaw_error < -180f) yaw_error += 360f;

            float k_yaw = 8f;
            float z_yaw = .98f;
            float p_yaw = .8f;

            yaw_output = k_yaw * yaw_error - k_yaw * z_yaw * prev_yaw_error + p_yaw * prev_yaw_output ; //Using a lead/lag controller with the defined gain, k, zero, z, and pole, p.
            if(yaw_output > 500){
                yaw_output = 500;
            }
            else if(yaw_output < -500){
                yaw_output = -500;
            }
            prev_yaw_error = yaw_error;
            prev_yaw_output = yaw_output;

            var FL_RPM = BaseRPM + alt_output + pitch_output - roll_output - yaw_output;
            var FR_RPM = BaseRPM + alt_output + pitch_output + roll_output + yaw_output;
            var BL_RPM = BaseRPM + alt_output - pitch_output - roll_output + yaw_output;
            var BR_RPM = BaseRPM + alt_output - pitch_output + roll_output - yaw_output;

            if(FL_RPM > MaxRPM || FR_RPM > MaxRPM || BL_RPM > MaxRPM || BR_RPM > MaxRPM){
                Debug.LogWarning("Target RPM outside bounds! FL: " + FL_RPM + " FR: " + FR_RPM + " BL: " + BL_RPM + " BR: " + BR_RPM);
                var available = MaxRPM - (BaseRPM + alt_output);
                float desired;
                if(FL_RPM > MaxRPM || FR_RPM > MaxRPM || BL_RPM > MaxRPM || BR_RPM > MaxRPM){
                    if(FL_RPM > MaxRPM){
                        desired = FL_RPM - (BaseRPM + alt_output);
                    }
                    else if(FR_RPM > MaxRPM){
                        desired = FR_RPM - (BaseRPM + alt_output);
                    }
                    else if(BL_RPM > MaxRPM){
                        desired = BL_RPM - (BaseRPM + alt_output);
                    }
                    else{
                        desired = BR_RPM - (BaseRPM + alt_output);
                    }
                    pitch_output *= available / desired;
                    roll_output *= available / desired;
                    yaw_output *= available / desired;

                    prev_yaw_output = yaw_output;
                    prev_pitch_output = pitch_output;
                    prev_roll_output = roll_output;

                    FL_RPM = BaseRPM + alt_output + pitch_output - roll_output - yaw_output;
                    FR_RPM = BaseRPM + alt_output + pitch_output + roll_output + yaw_output;
                    BL_RPM = BaseRPM + alt_output - pitch_output - roll_output + yaw_output;
                    BR_RPM = BaseRPM + alt_output - pitch_output + roll_output - yaw_output;
                    if(FL_RPM > MaxRPM || FR_RPM > MaxRPM || BL_RPM > MaxRPM || BR_RPM > MaxRPM){
                        Debug.LogWarning("Target RPM still too high! FL: " + FL_RPM + " FR: " + FR_RPM + " BL: " + BL_RPM + " BR: " + BR_RPM);
                    }
                }
            }
            
            FL.SetRpm(FL_RPM);
            FR.SetRpm(FR_RPM);
            BL.SetRpm(BL_RPM);
            BR.SetRpm(BR_RPM);
            if(log_data){
                using (StreamWriter sw = File.AppendText(data_path)){
                        sw.WriteLine($"{Time.fixedTime},{comRoll},{comPitch},{comYaw},{target_roll},{target_pitch},{target_yaw},{FLUVel.x},{FLUVel.y},{FLUVel.z},{CommandVelocityFLU.x},{CommandVelocityFLU.y},{CommandVelocityFLU.z}, {FL_RPM}, {FR_RPM}, {BL_RPM}, {BR_RPM}, {target_pitch}, {target_roll}");
                }
            }
        }

        Vector3 _normalize_max_speed(Vector3 speed){
            var mag = Mathf.Sqrt(speed.x * speed.x + speed.y * speed.y + speed.z * speed.z);
            if(mag > MaxENUSpeed){
                speed = speed / mag * MaxENUSpeed;
            }

            return speed;
        }
    }
}