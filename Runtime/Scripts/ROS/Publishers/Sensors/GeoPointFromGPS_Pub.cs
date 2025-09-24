using UnityEngine;
using RosMessageTypes.Geographic;

using SensorGPS = VehicleComponents.Sensors.GPS;
using ROS.Core;


namespace ROS.Publishers
{
    [RequireComponent(typeof(SensorGPS))]
    class GeoPointFromGPS_Pub: ROSSensorPublisher<GeoPointMsg, SensorGPS>
    { 
        void OnValidate()
        {
            ignoreSensorState = true;
        }

        protected override void UpdateMessage()
        {        
            var (_, _, lat, lon) = DataSource.GetUTMLatLon();
            ROSMsg.latitude = lat;
            ROSMsg.longitude = lon;
        }
    }
}