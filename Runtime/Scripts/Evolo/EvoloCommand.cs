using UnityEngine;
using VehicleComponents.Actuators;
using RosMessageTypes.Geometry;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;


namespace ROS.Subscribers
{

    [RequireComponent(typeof(IGenericTwistActuator))]
    [RequireComponent(typeof(EvoloCommand_sub))]
    public class EvoloCommand : MonoBehaviour
    {
        IGenericTwistActuator twistAct;
        public EvoloCommand_sub steering_sub;
        public EvoloCommand_sub speed_sub;

        
        public void Start()
        {
            twistAct = GetComponent<IGenericTwistActuator>();
        }

        private void FixedUpdate()
        {
            if(twistAct == null)
            {
                Debug.Log($"EvoloTwistCommand_Sub found no IGenericTwistActuator to command! Disabling.");
                return;
            }

            // ROS twist to Unity twist
            // FLU (ROS) to RUF (Unity)
            var linear = speed_sub.value;
            var angular = steering_sub.value;
            twistAct.SetTwist(
                FLU.ConvertToRUF(new Vector3(
                    (float)linear,
                    (float)0,
                    (float)0
                )),
                FLU.ConvertAngularVelocityToRUF(new Vector3(
                    (float)0,
                    (float)0,
                    (float)angular
                ))
            );
        }
    }
}
