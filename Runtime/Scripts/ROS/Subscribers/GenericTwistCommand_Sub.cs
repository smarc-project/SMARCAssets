using UnityEngine;
using VehicleComponents.Actuators;
using RosMessageTypes.Geometry;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;


namespace ROS.Subscribers
{

    [RequireComponent(typeof(IGenericTwistActuator))]
    public class GenericTwistCommand_Sub : Actuator_Sub<TwistStampedMsg>
    {
        IGenericTwistActuator[] twistActuators;

        void Awake()
        {
            twistActuators = GetComponents<IGenericTwistActuator>();
        }

        protected override void UpdateVehicle(bool reset)
        {
            if(twistActuators == null || twistActuators.Length == 0)
            {
                Debug.Log($"GenericTwistCommand_Sub found no IGenericTwistActuator to command! Disabling.");
                enabled = false;
                rosCon.Unsubscribe(topic);
                return;
            }

            if(reset)
            {
                foreach(var actuator in twistActuators)
                {
                    actuator.SetTwist(actuator.GetResetValue().Item1, actuator.GetResetValue().Item2);
                }
                return;
            }

            // ROS twist to Unity twist
            // FLU (ROS) to RUF (Unity)
            var linear = ROSMsg.twist.linear;
            var angular = ROSMsg.twist.angular;
            var linearRUF = FLU.ConvertToRUF(new Vector3(
                (float)linear.x,
                (float)linear.y,
                (float)linear.z
            ));
            var angularRUF = FLU.ConvertAngularVelocityToRUF(new Vector3(
                (float)angular.x,
                (float)angular.y,
                (float)angular.z
            ));
            foreach(var actuator in twistActuators)
            {
                actuator.SetTwist(linearRUF, angularRUF);
            }
        }
    }
}