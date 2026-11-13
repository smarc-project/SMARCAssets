using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;

namespace VehicleComponents.Sensors
{
    public class SonarHit
    {
        public RaycastHit Hit;
        public float ReturnIntensity;
        public int MaterialLabel;
        public bool IsDetected { get; private set; }
        readonly Sonar sonar;

        public static readonly Dictionary<string, float> simpleMaterialReflectivity = new Dictionary<string, float>()
        {
            {"Rock", 0.8f},
            {"Mud", 0.2f},
            {"Buoy", 0.99f},
            {"Algae", 0.25f},
            {"Rope", 0.4f}
        };

        public static readonly Dictionary<string, int> materialLabels = new Dictionary<string, int>()
        {
            {"Rock", 1},
            {"Mud", 1},
            {"Buoy", 3},
            {"Algae", 2},
            {"Rope", 4}
        };

        public SonarHit(Sonar sonar)
        {
            ReturnIntensity = -1;
            MaterialLabel = 0;
            this.sonar = sonar;
        }

        public void Update(RaycastHit hit, float beam_intensity)
        {
            IsDetected = hit.collider != null;
            // Reused raycast buffers can retain point/distance data for a miss.
            Hit = IsDetected ? hit : default;
            ReturnIntensity = GetIntensity(beam_intensity);
            MaterialLabel = GetMaterialLabel();
        }

        public void UpdateFromModel(RaycastHit hit, float returnIntensity, bool detected)
        {
            Hit = detected ? hit : default;
            IsDetected = detected;
            ReturnIntensity = detected ? returnIntensity : 0f;
            MaterialLabel = GetMaterialLabel();
        }

        static string CleanUpMaterialName(string name)
        {
            if(name.Contains("(")) return name.Split("(")[0].Trim();
            return name;
        }

        public float GetMaterialReflectivity()
        {
            if(!(Hit.collider)) return 0f;
            if(!(Hit.collider.material)) return 0.5f;

            string name = CleanUpMaterialName(Hit.collider.material.name);
            if(simpleMaterialReflectivity.ContainsKey(name)) return simpleMaterialReflectivity[name];
            return 0.5f;
        }
        
        public int GetMaterialLabel()
        {
            if(!(Hit.collider)) return 0;
            if(!(Hit.collider.material)) return 0;

            string name = CleanUpMaterialName(Hit.collider.material.name);
            if(materialLabels.ContainsKey(name)) return materialLabels[name];
            return 0;
        }

        public float GetIntensity(float beamIntensity)
        {
            float hitDistIntensity = (sonar.MaxRange - Hit.distance) / sonar.MaxRange;
            float hitAngle = Vector3.Angle(sonar.transform.position - Hit.point, Hit.normal);
            float hitAngleIntensity = Mathf.Abs(Mathf.Cos(hitAngle*Mathf.Deg2Rad));
            float hitMaterialIntensity = GetMaterialReflectivity();
            float intensity = beamIntensity * hitDistIntensity * hitAngleIntensity * hitMaterialIntensity;
            if(intensity > 1) intensity=1;
            if(intensity < 0) intensity=0;
            return intensity;
        }

        public byte[] GetBytes()
        {
            // Keep one point per ray; NaN marks a rejected or missing return in PointCloud2.
            var point = (IsDetected ? Hit.point : new Vector3(float.NaN, float.NaN, float.NaN)).To<ENU>();

            var xb = BitConverter.GetBytes(point.x);
            var yb = BitConverter.GetBytes(point.y);
            var zb = BitConverter.GetBytes(point.z);

            byte[] ib = {(byte)(ReturnIntensity*255)};

            int totalBytes = xb.Length + yb.Length + zb.Length+ ib.Length;
            byte[] ret = new byte[totalBytes];
            Buffer.BlockCopy(xb, 0, ret, 0, 4);
            Buffer.BlockCopy(yb, 0, ret, 4, 4);
            Buffer.BlockCopy(zb, 0, ret, 8, 4);
            Buffer.BlockCopy(ib, 0, ret, 12,1);

            return ret;
        }
    }


}
