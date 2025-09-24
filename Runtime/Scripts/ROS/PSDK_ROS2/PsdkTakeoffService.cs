using UnityEngine;
using RosMessageTypes.Std;
using dji;

using ROS.Core;


namespace M350.PSDK_ROS2
{
    public class PsdkTakeoffService : ROSBehaviour
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
                rosCon.ImplementService<TriggerRequest, TriggerResponse>(topic, _takeoff_callback);
                registered = true;
            }
        }

        private TriggerResponse _takeoff_callback(TriggerRequest request)
        {
            TriggerResponse response = new TriggerResponse();
            Debug.Log("Take off service running");
            if (controller == null)
            {
                Debug.Log("Finding Controller Component");
                controller = GetComponentInParent<DJIController>();
                if (controller == null)
                {
                    Debug.Log("Controller not found");
                    response.success = false;
                    return response;
                }
            }

            response.success = controller.TakeOff();
            return response;
        }

    }
}