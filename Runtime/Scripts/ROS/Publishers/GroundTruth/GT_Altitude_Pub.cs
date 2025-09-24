using UnityEngine;
using ROS.Core;
using RosMessageTypes.Std;
using DefaultNamespace.Water;


namespace ROS.Publishers.GroundTruth
{
    class GT_Altitude_Pub : ROSPublisher<Float32Msg>
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
            float altitude = base_link.position.y;
            // if we are underwater, we need a raycast down to get altitude from the ground
            // if there is no hit, that means we are underwater, but there is no ground...
            // so infinite altitude?
            if (depth > 0)
            {
                if (Physics.Raycast(base_link.position, Vector3.down, out RaycastHit hit))
                {
                    altitude = hit.distance;
                }
                else
                {
                    altitude = float.PositiveInfinity;
                }
            }
            ROSMsg.data = altitude;
        }
    }
}
