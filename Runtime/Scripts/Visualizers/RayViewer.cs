using System;

using UnityEngine;

using Sonar = VehicleComponents.Sensors.Sonar;

namespace Visualizers
{

    public class RayViewer : MonoBehaviour
    {
        Sonar sonar;

        [Header("Rays")]
        [Tooltip("Draw rays in the scene view as lines?")]
        public bool DrawRays = false;
        public Material RayMaterial;
        public float RayThickness = 0.05f;
        [Tooltip("Sample every Nth ray in row and column. 4 draws roughly 1/16 of the beam grid.")]
        public int RayDrawStride = 4;

        [Header("Hits")]
        [Tooltip("Just draw the hit points as particles?")]
        public bool DrawHits = false;
        [Tooltip("Drawing hits every single frame can create A LOT of points. If you want to visualize a large area fast, you maybe don't need 1mm density :)")]
        public int DrawEveryNthFrame = 10;
        [Tooltip("Assign a mesh object to be drawn at every hit point. Assign something with few verts, like a quad or triangle, a sphere at most.")]
        public bool UseRainbow = false;
        public float HitsSize = 0.1f;
        [Tooltip("How many seconds should the hits be drawn? Limited by MaxParticlesMultiplier too.")]
        public float HitsLifetime = 1f;
        [Tooltip("How many sets of rays should we allow to be drawn? Limited by lifetime too.")]
        public int MaxParticlesMultiplier = 10;

        GameObject RayDrawer;
        LineRenderer RaysLR;
        Vector3[] rayPositions;
        int cachedRayHorizontalResolution;
        int cachedRayVerticalResolution;
        int cachedRayDrawStride;

        GameObject HitsDrawer;
        ParticleSystem HitsParticleSystem;
        ParticleSystem.EmitParams hitsEmitParams;
        int HitsSkipped = 0;


        public static Color Rainbow(float progress)
        {
            float div = Math.Abs(progress % 1) * 6;
            int ascending = (int) ((div % 1) * 255);
            int descending = 255 - ascending;

            static Color FromArgb (int alpha, int red, int green, int blue)
            {
                float fa = ((float)alpha) / 255.0f;
                float fr = ((float)red)   / 255.0f;
                float fg = ((float)green) / 255.0f;
                float fb = ((float)blue)  / 255.0f;
                return new Color(fr,fg,fb,fa);
            }

            switch ((int) div)
            {
                case 0:
                    return FromArgb(255, 255, ascending, 0);
                case 1:
                    return FromArgb(255, descending, 255, 0);
                case 2:
                    return FromArgb(255, 0, 255, ascending);
                case 3:
                    return FromArgb(255, 0, descending, 255);
                case 4:
                    return FromArgb(255, ascending, 0, 255);
                default: // case 5:
                    return FromArgb(255, 255, 0, descending);
            }
        }

        void Start()
        {
            sonar = GetComponent<Sonar>();
            if(DrawRays)
            {
                RayDrawer = new GameObject("RayDrawer");
                RayDrawer.transform.SetParent(transform);
                RaysLR = RayDrawer.AddComponent<LineRenderer>();
                RaysLR.material = RayMaterial;
                RaysLR.startWidth = RayThickness;
                RaysLR.endWidth = RayThickness;
                RaysLR.receiveShadows = false;
                RaysLR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            if(DrawHits)
            {
                HitsDrawer = new GameObject("HitsDrawer");
                HitsDrawer.transform.SetParent(transform);
                HitsParticleSystem = HitsDrawer.AddComponent<ParticleSystem>();
                
                var rendererModule = HitsDrawer.GetComponent<ParticleSystemRenderer>();
                var mat = Shader.Find("Particles/Standard Unlit");
                if(mat == null) 
                {
                    DrawHits = false;
                    Debug.Log($"Material 'Particles/Standard Unlit' not found, sonar hits wont be drawn. Found: {mat}");
                    HitsDrawer.SetActive(false);
                }
                else
                {
                    rendererModule.material = new Material(mat);
                    rendererModule.alignment = ParticleSystemRenderSpace.World;
                    rendererModule.sortMode = ParticleSystemSortMode.YoungestInFront;
                    rendererModule.renderMode = ParticleSystemRenderMode.Mesh;
                    rendererModule.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

                    
                    var mainModule = HitsParticleSystem.main;
                    mainModule.startSpeed = 0f;
                    mainModule.playOnAwake = false;
                    mainModule.maxParticles = sonar.TotalRayCount * MaxParticlesMultiplier;
                    mainModule.startColor = Color.red;
                    mainModule.startSize = HitsSize;
                    mainModule.startLifetime = HitsLifetime;
                    mainModule.simulationSpace = ParticleSystemSimulationSpace.World;
                    mainModule.ringBufferMode = ParticleSystemRingBufferMode.PauseUntilReplaced;

                    var emissionModule = HitsParticleSystem.emission;
                    emissionModule.enabled = false;
                    var shapeModule = HitsParticleSystem.shape;
                    shapeModule.enabled = false;  
                    
                }

            }
        }

        void EnsureRayPositionBuffer(int horizontalResolution, int verticalResolution, int stride)
        {
            if (rayPositions != null
                && cachedRayHorizontalResolution == horizontalResolution
                && cachedRayVerticalResolution == verticalResolution
                && cachedRayDrawStride == stride)
                return;

            cachedRayHorizontalResolution = horizontalResolution;
            cachedRayVerticalResolution = verticalResolution;
            cachedRayDrawStride = stride;

            int sampledRows = (verticalResolution + stride - 1) / stride;
            int sampledCols = (horizontalResolution + stride - 1) / stride;
            rayPositions = new Vector3[sampledRows * sampledCols * 2];
        }

        void UpdateRays()
        {
            if (RaysLR == null) return;
            if (!DrawRays)
            {
                RaysLR.enabled = false;
                return;
            }

            RaysLR.enabled = true;
            if (sonar.SonarHits == null) return;

            int stride = Mathf.Max(1, RayDrawStride);
            int horizontal = sonar.HorizontalResolution;
            int vertical = sonar.VerticalResolution;
            EnsureRayPositionBuffer(horizontal, vertical, stride);

            Vector3 origin = sonar.transform.position;
            int positionCount = 0;
            for (int row = 0; row < vertical; row += stride)
            {
                for (int col = 0; col < horizontal; col += stride)
                {
                    int i = row * horizontal + col;
                    if (i >= sonar.SonarHits.Length || !sonar.SonarHits[i].IsDetected) continue;

                    rayPositions[positionCount++] = origin;
                    rayPositions[positionCount++] = sonar.SonarHits[i].Hit.point;
                }
            }

            RaysLR.positionCount = positionCount;
            RaysLR.SetPositions(rayPositions);
        }

        void UpdateHits()
        {
            if (!DrawHits || HitsParticleSystem == null) return;
            
            for (int i = 0; i < sonar.TotalRayCount; i++)
            {
                if (!sonar.SonarHits[i].IsDetected) continue;
                var hitPoint = sonar.SonarHits[i].Hit.point;
                var surfaceNormal = sonar.SonarHits[i].Hit.normal;
                if(surfaceNormal == Vector3.zero) surfaceNormal = Vector3.up;

                float normalizedZ = Mathf.InverseLerp(sonar.HitsMaxHeight, sonar.HitsMinHeight, hitPoint.y);
                hitsEmitParams.startColor = UseRainbow ? Rainbow(normalizedZ) : Color.red;
                hitsEmitParams.position = hitPoint + 0.03f * surfaceNormal;
                hitsEmitParams.rotation3D = Quaternion.LookRotation(-surfaceNormal).eulerAngles;
                hitsEmitParams.startSize = HitsSize;
                hitsEmitParams.startLifetime = HitsLifetime;

                HitsParticleSystem.Emit(hitsEmitParams, 1);
            }
                
        }

        void Update()
        {
            UpdateRays();
            if(HitsSkipped == 0) UpdateHits();
            else HitsSkipped = (HitsSkipped+1)%DrawEveryNthFrame;
        }

        
    }

}
