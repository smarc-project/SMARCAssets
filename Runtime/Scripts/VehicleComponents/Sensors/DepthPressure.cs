using UnityEngine;
using DefaultNamespace.Water; // WaterQueryModel

namespace VehicleComponents.Sensors
{
    public class DepthPressure: Sensor
    {
        [Header("Depth-Pressure")]
        public float maxDepth;
        public bool includeAtmosphericPressure;
        public float pressure;
        private WaterQueryModel _waterModel;

        void Start()
        {
            var waterModels = FindObjectsByType<WaterQueryModel>(FindObjectsSortMode.None);
            if(waterModels.Length > 0) _waterModel = waterModels[0];
            else 
            {
                Debug.LogWarning("DepthPressure: No WaterQueryModel found in the scene, disabling sensor");
                enabled = false;
            }

        }


        public override bool UpdateSensor(double deltaTime)
        {
            var waterSurfaceLevel = _waterModel.GetWaterLevelAt(transform.position);
            float depth = waterSurfaceLevel - transform.position.y;
            if (includeAtmosphericPressure) pressure = 101325.0f;
            else pressure = 0;

            // 1m water = 9806.65 Pa
            if (depth > maxDepth) return false;
            else
            {
                pressure += depth * 9806.65f;
                return true;
            }
            
        }
    }
}