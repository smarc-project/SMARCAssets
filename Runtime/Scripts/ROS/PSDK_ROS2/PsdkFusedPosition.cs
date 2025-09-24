using Unity.Robotics.Core;
using UnityEngine;
using RosMessageTypes.PsdkInterfaces;
using dji;
using ROS.Publishers;
using ROS.Core;



namespace M350.PSDK_ROS2
{
    public class PsdkFusedPos : ROSPublisher<PositionFusedMsg>
    {
        private DJIController controller = null;
        private OdomFromIMU_Pub odom = null;
        
        protected override void InitPublisher(){
            base.InitPublisher();
            controller = GetComponentInParent<DJIController>(); //Get current control state from the controller itself
            if(controller !=null){
                odom = controller.GetComponentInChildren<OdomFromIMU_Pub>();
            }
        }

        protected override void UpdateMessage(){
            Vector3 ROSPosition = Vector3.zero;

            if(controller == null){
                controller = GetComponentInParent<DJIController>();
            }
            if(controller !=null && odom == null){
                odom = controller.GetComponentInChildren<OdomFromIMU_Pub>();
                
            }
            if(odom != null){
                    ROSPosition = odom.ROSPosition;  
            }

            ROSMsg.position.x = ROSPosition.x;
            ROSMsg.position.y = ROSPosition.y;
            ROSMsg.position.z = ROSPosition.z;
            ROSMsg.header.frame_id = "psdk_map_enu";
            ROSMsg.header.stamp = new TimeStamp(Clock.time);
        }
    }
}