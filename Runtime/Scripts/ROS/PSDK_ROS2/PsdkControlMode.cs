using RosMessageTypes.PsdkInterfaces;
using dji;
using ROS.Core;



namespace M350.PSDK_ROS2
{
    public class PsdkControlMode : ROSPublisher<ControlModeMsg>
    {
        private DJIController controller = null;

        protected override void InitPublisher(){
            controller = GetComponentInParent<DJIController>(); //Get current control state from the controller itself
        }

        protected override void UpdateMessage()
        {
            if(controller == null){
                controller = GetComponentInParent<DJIController>();
            }
            if(controller != null){
                if(controller.ControllerType == ControllerType.FLU_Attitude)
                {
                    ROSMsg.control_auth = 0;
                }
                else
                {
                    ROSMsg.control_auth = 1;
                }
                ROSMsg.device_mode = 4;
            }
        }
    }
}