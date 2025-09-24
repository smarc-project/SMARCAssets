using UnityEngine;
using ROS.Core;
using RosMessageTypes.Std;
using DefaultNamespace.Water;


namespace ROS.Publishers.GroundTruth
{
    class GT_Depth_Pub : ROSPublisher<Float32Msg>
    {
        Transform base_link;
        WaterQueryModel waterQueryModel;


        protected override void InitPublisher()
        {
            if (GetBaseLink(out base_link))
            {
                waterQueryModel = FindFirstObjectByType<WaterQueryModel>();
                if (waterQueryModel == null)
                {
                    Debug.LogError("WaterQueryModel not found in the scene.");
                    enabled = false;
                    return;
                }
            }
        }

        protected override void UpdateMessage()
        {
            var waterSurfaceLevel = waterQueryModel.GetWaterLevelAt(base_link.position);
            float depth = waterSurfaceLevel - base_link.position.y;
            ROSMsg.data = depth;
        }
    }
}
