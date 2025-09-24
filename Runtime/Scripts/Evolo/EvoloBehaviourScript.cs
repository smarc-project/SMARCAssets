using UnityEngine;
using System; //for math
using UnityEngine.UI;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;
using DefaultNamespace.Water;
namespace Evolo 
{

    public class BoatController : MonoBehaviour
    {
        private ROSConnection ros;
        public Rigidbody BaseLinkRB;

        // Control Inputs
        public float linearSpeedGoalKt = 0f;  // Speed in knots (-X, 0, or 8-13 knots)
        public float rollAngleGoal = 0f;    // Roll input in rad
        private float rollAngleGoalprivate = 0f;    // Roll input in rad
        public bool useROSCommands = true; // Default: Using ROS commands
        public string subscribeTopic = "/evolo_cmd";
        private string privateSubscribeTopic="/evolo_cmd";
        public float maxRollAceleration=15; //rad/s^2
        public float heightCorrection=-0.9f; 


        public float speedMetersPerSecond=0f;
        private float RollAngle;
        //private float pitch = 0f;
        private const float knotsToMetersPerSecond = 0.51444f;
        private const float gravity = 9.81f;

        private WaterQueryModel waterModel;
        private float waterSurfaceLevel = 0f;
        private float currentBoatOffsetZ = 0f;
        private const float boatOffsetZ = 0.35f; // 35 cm offset from water surface for 8-13knots - less than that for less speed
        private const float minSpeed=8f;
        private const float minNegSpeed=-3f;
        private const float maxSpeed=13f; // Speed [m/s] - typically we fly at 8-13 knots
        private const float maxRoll=13f; //Roll [deg]  - typically we turn at ~13 degrees roll angle
        private const float maxBoatAceleration= 10 *knotsToMetersPerSecond / 5; //Acceleration/decelleration from 0 to 10kn is done in ~ 5 seconds
        private float currentLinearSpeed=0f;
        private float currentRollAngle =0f;


        void OnValidate()
        {
            if (gameObject.scene.IsValid() && enabled) Debug.LogWarning("This is OnValidate on EvoloBehaviourScript. It needs to be cleaned up and inspected. Use at your own risk!");
            // the topic switching is weird?!
            // Unity control and ROS Control in the same script?!?!?!
            // Unity control not using input manager?!?!!?!?!?!?!?!?!?
            // lots of todos here....
        }


        void Start()
        {
            ros = ROSConnection.GetOrCreateInstance();
            ros.Subscribe<TwistMsg>(subscribeTopic, UpdateBoatControl);
            
            // Find the water model in the scene
            var waterModels = FindObjectsByType<WaterQueryModel>(FindObjectsSortMode.None);
            if (waterModels.Length > 0)
                waterModel = waterModels[0];
        }

        void Update()
        {
            if (!useROSCommands) // Unity control mode
            {
                Unity_control_speed_yaw();
            }
            if (privateSubscribeTopic!=subscribeTopic){ //alternate between topics to control evolo
                ros.Unsubscribe(privateSubscribeTopic);
                ros.Subscribe<TwistMsg>(subscribeTopic, UpdateBoatControl);
                privateSubscribeTopic=subscribeTopic;
                Debug.Log($"Changed topic on which evolo is controlled. Now listening to topic:  {subscribeTopic }");

            }
        }

        void UpdateBoatControl(TwistMsg msg)
        {
            if (useROSCommands) // Only update if ROS mode is enabled
            {
                float difference_speed= (float)msg.linear.x - linearSpeedGoalKt;
                speed_roll_limits((float)msg.linear.x,difference_speed,(float)msg.angular.z);
            }
        }

        void FixedUpdate()
        {
            currentLinearSpeed = Compute_with_aceleration(currentLinearSpeed, linearSpeedGoalKt,Time.fixedDeltaTime,maxBoatAceleration/knotsToMetersPerSecond);

            speedMetersPerSecond = currentLinearSpeed * knotsToMetersPerSecond; //current speed

            float currentRollDegrees = BaseLinkRB.rotation.eulerAngles.z;
            if (currentRollDegrees > 180f) currentRollDegrees -= 360f; // Convert to [-180, 180]

            currentRollAngle = Compute_with_aceleration(currentRollDegrees, rollAngleGoalprivate, Time.fixedDeltaTime, maxRollAceleration);
            
            //currentRollAngle = Compute_with_aceleration(rb.rotation.eulerAngles.z ,rollAngleGoal,Time.fixedDeltaTime,maxRollAceleration );
            float yawRate = ComputeYawRate(currentRollAngle, speedMetersPerSecond);
            
            currentBoatOffsetZ =OffsetZ(speedMetersPerSecond);

            // Get water level at the boat's position
            if (waterModel != null)
                waterSurfaceLevel = waterModel.GetWaterLevelAt(BaseLinkRB.transform.position);

            MoveBoat(yawRate);
        }

        void MoveBoat(float yawRate)
        {
            //Debug.Log($"transform.forward * speedMetersPerSecond * Time.fixedDeltaTime;: {transform.forward * speedMetersPerSecond * Time.fixedDeltaTime }");
            //Debug.Log($"speedMetersPerSecond  {speedMetersPerSecond }");

            // Update position (x, y) using forward velocity
            Vector3 newPosition = BaseLinkRB.position + BaseLinkRB.transform.forward * speedMetersPerSecond * Time.fixedDeltaTime;
        
            // Update z position (height) to match water level
            newPosition.y = waterSurfaceLevel + currentBoatOffsetZ + heightCorrection;

            // Apply movement using Rigidbody (recommended for kinematic Rigidbody)
            BaseLinkRB.MovePosition(newPosition);
            

            // Get current yaw in radians
            float currentYaw = BaseLinkRB.rotation.eulerAngles.y * Mathf.Deg2Rad;

            // Compute new yaw angle
            float newYaw = currentYaw + (yawRate * Time.fixedDeltaTime);

            // Convert back to degrees for Quaternion
            newYaw *= Mathf.Rad2Deg; 

            // Compute new rotation (yaw, pitch, roll)
            Quaternion newRotation = Quaternion.Euler(BaseLinkRB.rotation.eulerAngles.x, newYaw, currentRollAngle);
            //Debug.Log($"newYaw  {newYaw }");

            // Apply rotation using Rigidbody
            BaseLinkRB.MoveRotation(newRotation);
        }

        [ContextMenu("Toggle Control Mode")]
        public void ToggleControlMode()
        {
            useROSCommands = !useROSCommands;
            Debug.Log("Control Mode: " + (useROSCommands ? "ROS" : "Unity"));
        }    
        float ComputeYawRate(float roll, float speed)
        {
            // Radius = Speed^2/(tan(roll)*9.81)
            //Roll [rad] - typically we turn at ~13 degrees roll angle
            //Speed [m/s] - typically we fly at 8-13 knots
            //Radius [m] - The lord gives us the turning radius
            //
            //yaw rate = speed/radius of turn
            //yaw rate = tan(roll) x g /speed
            if (speed<(minSpeed-0.5)*knotsToMetersPerSecond){
                return 0;

            } else {
            return -Mathf.Tan(roll*Mathf.Deg2Rad) * gravity / speed;  
            }
        }

        float Compute_with_aceleration(float current, float goal, float delta_t, float max_accel)
        {
            float goal_accel=goal - current;

            float delta= Mathf.Min(Mathf.Abs(goal_accel),max_accel*delta_t);
            if (goal_accel==0){
                return current;
            }else{
                delta*=goal_accel/Mathf.Abs(goal_accel); //just to get sign from goal aceel
                return current + delta;
            }
        }


        /*float PIDRoll(float current_roll,float goal_roll, float delta_t)
        {   Does not currently work - needs a simulated sytem to be controlled, that represents a roll inertia
            float error = goal_roll - current_roll;

            // Proportional term
            float P = Kp * error;

            // Integral term (accumulates over time)
            integral += error * delta_t;
            float I = Ki * integral;

            // Derivative term (rate of change of error)
            float derivative = (error - previousError) / delta_t;
            float D = Kd * derivative;
            // Update previous error for next iteration
            previousError = error;

            // Compute final control output
            //return goal_roll;
            return (P + I + D)*angularInertia + current_roll;

        }

        float RollProportinal(float current_roll,float goal_roll)
        {
            float error = goal_roll - current_roll;

            return Kp*error + current_roll;
        }*/


        float OffsetZ(float speed)
        {
            if (speed<0){
                return 0;
            }
            if (speed<minSpeed)
            {
                return boatOffsetZ *speed/minSpeed;
            }
            else {
                return boatOffsetZ;
            }
        }

        void speed_roll_limits(float added_speed,float difference_speed,float added_roll)
        {
            if (linearSpeedGoalKt<0){
                linearSpeedGoalKt = Mathf.Clamp(added_speed, minNegSpeed, 0f);
            } else if (linearSpeedGoalKt==0){
                if (difference_speed>0){
                    linearSpeedGoalKt = minSpeed;
                    } else {
                    linearSpeedGoalKt += difference_speed;
                }
            } else { //linearSpeedGoalKt >= 8
                if (difference_speed<0 && linearSpeedGoalKt==minSpeed){
                    linearSpeedGoalKt = 0;
                    } else {
                    linearSpeedGoalKt = Mathf.Clamp(added_speed, minSpeed, maxSpeed);
                }
                
            }
            rollAngleGoal = Mathf.Clamp(added_roll, -maxRoll, maxRoll);
            if (currentLinearSpeed<minSpeed-1){
                rollAngleGoalprivate=0;
                }else {
                    rollAngleGoalprivate = rollAngleGoal;
                }
        }
        void Unity_control_speed_yaw()
        {
            float unitySpeedInput = Input.GetAxis("Vertical")/5; // "W/S" keys
            float unityRollInput = -Input.GetAxis("Horizontal")/1.5f; // "A/D" keys
            
            float added_speed = linearSpeedGoalKt+unitySpeedInput ;
            float difference_speed = unitySpeedInput;
            float added_roll = rollAngleGoal+unityRollInput;

            speed_roll_limits(added_speed,difference_speed,added_roll);
        }
    }
}