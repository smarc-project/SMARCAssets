using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;
using VehicleComponents.ROS.Core; 

using Rope;

namespace VehicleComponents.ROS.Subscribers
{
    [RequireComponent(typeof(Winch))]
    public class Winch_Sub : ROSBehaviour
    {
        Winch winch;

        protected override void StartROS()
        {
            Debug.Log("[ROS] Winch_Sub StartROS called");
            winch = GetComponent<Winch>();
            rosCon.Subscribe<Float32MultiArrayMsg>("/winch_control_test", HandleTestControl);
        }


        void HandleTestControl(Float32MultiArrayMsg msg)
        {
            if (msg.data.Length < 2) return;

            float target = msg.data[0];
            float speed = msg.data[1];
            winch.TargetLength = Mathf.Clamp(target, winch.MinLength, winch.RopeLength);
            winch.WinchSpeed = speed;
        }
    }
}

// using UnityEngine;
// using RosMessageTypes.Std;
// using VehicleComponents.ROS.Core;
// using Rope;

// namespace VehicleComponents.ROS.Subscribers
// {
//     [RequireComponent(typeof(Winch))]
//     public class Winch_Sub : Actuator_Sub<Float32MultiArrayMsg>
//     {
//         private Winch winch;

//         protected override void StartROS()
//         {
//             base.StartROS();  // Ensure base subscription logic runs

//             winch = GetComponent<Winch>();
//             if (winch == null)
//             {
//                 Debug.LogError("[ROS] Winch component not found on GameObject.");
//                 enabled = false;
//                 return;
//             }

//             Debug.Log("[ROS] Winch_Sub StartROS completed");
//         }

//         protected override void UpdateVehicle(bool reset)
//         {
//             if (reset)
//             {
//                 Debug.LogWarning("[ROS] Winch_Sub reset triggered due to low message frequency.");
//                 return; // Optional: implement a reset behavior here
//             }

//             var data = ROSMsg.data;
//             if (data == null || data.Length < 2)
//             {
//                 Debug.LogWarning("[ROS] Invalid Float32MultiArray received in Winch_Sub.");
//                 return;
//             }

//             float target = data[0];
//             float speed = data[1];

//             winch.TargetLength = Mathf.Clamp(target, winch.MinLength, winch.RopeLength);
//             winch.WinchSpeed = speed;

//             // Optional debug:
//             Debug.Log($"[ROS] Winch command received: target={target}, speed={speed}");
//         }
//     }
// }


