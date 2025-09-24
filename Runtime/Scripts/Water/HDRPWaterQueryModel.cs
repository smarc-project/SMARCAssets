using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace DefaultNamespace.Water
{
    public class HDRPWaterQueryModel : WaterQueryModel
    {
        public WaterSurface water;
        WaterSearchResult result;

        public void Awake()
        {
            water = FindObjectsByType<WaterSurface>(FindObjectsSortMode.None)[0];
            if (water == null) Debug.Log("Water object not found!");
        }

        public override float GetWaterLevelAt(Vector3 position)
        {
            WaterSearchParameters parameters = new WaterSearchParameters();

            parameters.startPositionWS = result.candidateLocationWS; //TODO: Probably want to cache this, but for current purposes most points will be close to each other. Not true with multiple vehicles. Might want a copy of model for each vehicle instead?
            parameters.targetPositionWS = position;
            parameters.maxIterations = 6;
            parameters.error = 0.01f;

            water.ProjectPointOnWaterSurface(parameters, out result);
            return result.projectedPositionWS.y;
        }
        
    }
}
