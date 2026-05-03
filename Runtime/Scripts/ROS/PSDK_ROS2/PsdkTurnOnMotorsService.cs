using RosMessageTypes.Std;
using dji;

using ROS.Core;
using UnityEngine;

namespace M350.PSDK_ROS2
{
    [AddComponentMenu("Smarc/PSDK_ROS/PsdkTurnOnMotorsService")]
    public class PsdkTurnOnMotorsService : ROSBehaviour
    {
        bool registered = false;
        DJIController controller = null;

        protected override void StartROS()
        {
            if(controller == null){
                controller = GetComponentInParent<DJIController>();
            }
            if (!registered)
            {
                rosCon.ImplementService<TriggerRequest, TriggerResponse>(topic, _turn_on_motors_callback);
                registered = true;
            }
        }

        private TriggerResponse _turn_on_motors_callback(TriggerRequest request){
            TriggerResponse response = new TriggerResponse();
            if (controller == null)
            {
                controller = GetComponentInParent<DJIController>();
                if (controller == null)
                {
                    Debug.Log("Controller not found");
                    response.success = false;
                    return response;
                }
            }

            response.success = controller.TurnOnMotors();
            return response;
        }
    }
}