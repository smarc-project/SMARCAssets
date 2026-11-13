using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using VehicleComponents.Sensors;

namespace SensorProtocols.Ixblue
{
    /// <summary>
    /// Thin bridge from existing Sensor objects to protocol-specific measurements.
    /// </summary>
    [AddComponentMenu("Smarc/Sensor Protocols/iXblue Measurement Adapter")]
    public sealed class IxblueMeasurementAdapter : MonoBehaviour
    {
        [Header("SMARC sensors")]
        public IMU imu;
        public GPS gps;

        public bool HasSources => imu != null && gps != null;

        public IxblueSample GetSample() 
        {
          if (!HasSources)
            throw new InvalidOperationException(
                "Assign both an IMU and GPS to the iXblue measurement adapter.");

          // Convert orientation to ROS FLU convention.
          var quaternion = imu.orientation.To<FLU>();

          // Calculate maritime heading, roll and pitch.
          Quaternion unityOrientation = imu.orientation;

          Vector3 forward = unityOrientation * Vector3.forward;
          Vector3 left = unityOrientation * Vector3.left;
          Vector3 up = unityOrientation * Vector3.up;

          float heading = Mathf.Repeat(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360f);

          float horizontalForward = Mathf.Sqrt(forward.x * forward.x + forward.z * forward.z);

          return new IxblueSample 
          {
            // Position.
            Latitude = gps.lat,
            Longitude = NormalizeLongitude(gps.lon),
            Altitude = (float)gps.alt,

            // Attitude.
            Heading = heading,
            Roll = Mathf.Atan2(left.y, up.y) * Mathf.Rad2Deg,
            Pitch = Mathf.Atan2(-forward.y, horizontalForward) * Mathf.Rad2Deg,

            // Quaternion.
            Q0 = -quaternion.w,
            Q1 = quaternion.x,
            Q2 = quaternion.y, Q3 = quaternion.z,

            // Motion (ROS FLU convention).
            AngularVelocity = imu.angularVelocity.To<FLU>() * Mathf.Rad2Deg,
            LinearAcceleration = imu.linearAcceleration.To<FLU>(),
            VesselVelocity = imu.localVelocity.To<FLU>()
          };
        }

        private static double NormalizeLongitude(double longitude)
        {
            return (longitude % 360.0 + 360.0) % 360.0;
        }
    }
}
