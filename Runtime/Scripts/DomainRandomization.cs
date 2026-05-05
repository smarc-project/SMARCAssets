using System.IO;
using SmarcGUI;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class DomainRandomization : MonoBehaviour
{
    [Header("Sun Light Settings")]
    [Tooltip("The sun light source in the scene")]
    public GameObject TheSun;
    Light sunLight;
    Transform sunTF;
    [Tooltip("Degrees")]
    public float SunRotationHorizontalBase = 0f;
    public float SunRotationHorizontalRange = 30f; // rotation.y
    public float SunRotationVerticalBase = 45f;
    public float SunRotationVerticalRange = 10f; // rotation.x
    [Tooltip("Kelvin")]
    public float SunEmissionTemperatureBase = 10000f; // Kelvin
    public float SunEmissionTemperatureRange = 2000f; // Kelvin
    [Tooltip("Thousands of Lux")]
    public float SunIntensityThousandsBase = 40f; // thousand Lux
    public float SunIntensityThousandsRange = 10f; // thousand Lux

    [Header("Sky and Fog Settings")]
    public GameObject SkyAndFogSettings;
    Volume skyAndFogVolume;
    Fog fog;
    PhysicallyBasedSky sky;
    [Range(0f, 1f)]
    public float AerosolDensityBase = 0f;
    public float AerosolDensityRange = 0.05f;
    public float FogDistanceBase = 1000f;
    public float FogDistanceRange = 20f;

    [Header("Water Surface Settings")]
    [Tooltip("The ocean or water surface in the scene, usually called Ocean")]
    public WaterSurface WaterSurface;
    public float DistantWindSpeedBase = 10f;
    public float DistantWindSpeedRange = 10f;
    public float LocalWindSpeedBase = 2f;
    public float LocalWindSpeedRange = 2f;
    public float CurrentOrientationBase = 0f;
    public float CurrentOrientationRange = 360f;
    public float CurrentSpeedBase = 1f;
    public float CurrentSpeedRange = 1f;
    public bool RGBOcean = true;

    [Header("Cameras")]
    public Transform CamTF;
    public float WaitForExposure = 2f;
    public int NumShotsPerExposure = 10;
    Camera[] cams;
    [Tooltip("If set, cameras will look at this target after moving")]
    public Transform LookAtTarget;
    [Tooltip("If true, cameras will be centered horizontally on the target position before applying random offsets")]
    public bool CenterCamerasHorizontallyOnTarget = true;
    [Tooltip("Randomize a relative offset to look at around the target")]
    public float LookAtTargetOffsetHorizontalRange = 3f;
    [Tooltip("Randomize a relative offset to look at around the target")]
    public float LookAtTargetOffsetVerticalRange = 1f;
    [Tooltip("Range in meters for randomizing camera positions around their original position")]
    public float CameraHorizontalPositionRange = 0.5f;
    public float CameraVerticalPositionRange = 0.5f;
    [Tooltip("Range in degrees for randomizing camera rotations")]
    public float CameraRotationRange = 10f;
    [Tooltip("Range in degrees for randomizing camera field of view")]
    public float CameraFOVBase = 80f;
    public float CameraFOVRange = 20f;

    [Header("Noise Material Randomization")]
    [Tooltip("Renderers that use the YellowNoise material")]
    public Renderer[] YellowNoiseRenderers;

    [Tooltip("Renderers that use the OrangeNoise material")]
    public Renderer[] OrangeNoiseRenderers;

    [Header("Noise Surface Randomization")]
    public bool RandomizeSurfaceAppearance = true;
    [Range(0f, 1f)] public float SurfaceRandomizationStrength = 1.0f; // 0=off-ish, 1=full


    [Header("Noise Color Strategy (Tuned for SAM/Buoy)")]
    [Range(0f, 1f)] public float WarmShiftedChance = 0.70f;   // 1) near but degraded
    [Range(0f, 1f)] public float HardNegativeChance = 0.15f;  // 2) close to target colors sometimes
    [Range(0f, 1f)] public float FarChance = 0.15f;           // 3) far colors (cool/neutral) occasionally

    // Colors:
    // SAM rgba(248,236,150) -> hue ~0.146
    // Buoy rgba(255,172,78) -> hue ~0.089
    public Vector2 TargetYellowHueRange = new Vector2(0.113f, 0.179f); // ~53° ±12°
    public Vector2 TargetOrangeHueRange = new Vector2(0.056f, 0.122f); // ~32° ±12°

    static readonly int MetallicID = Shader.PropertyToID("_Metallic");
    static readonly int SmoothnessID = Shader.PropertyToID("_Smoothness");

    // HDRP/Lit base color property
    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    MaterialPropertyBlock _mpb;

    [Header("Files")]
    public int NumImages = 100;
    int shotIndex = 0;
    string sessionPath;
    StreamWriter infoWriter;


    void SetComponents()
    {
        _mpb ??= new MaterialPropertyBlock();

        if (TheSun != null)
        {
            sunLight = TheSun.GetComponent<Light>();
            sunTF = TheSun.transform;
        }
        if (SkyAndFogSettings != null)
        {
            skyAndFogVolume = SkyAndFogSettings.GetComponent<Volume>();
            skyAndFogVolume.profile.TryGet(out fog);
            skyAndFogVolume.profile.TryGet(out sky);
        }
        cams = CamTF.GetComponentsInChildren<Camera>();
    }

    void Start()
    {
        SetComponents();
        // for each camera under the CamTF object, set the render target to a new texture with the same resolution as the camera
        foreach (Camera cam in cams)
        {
            RenderTexture rt = new(cam.pixelWidth, cam.pixelHeight, 24);
            cam.targetTexture = rt;
        }
        string date = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string mainStoragePath = Path.Combine(GUIState.GetStoragePath(), "DomainRandomization");
        sessionPath = Path.Combine(mainStoragePath, date);
        Directory.CreateDirectory(sessionPath);

        string infoPath = Path.Combine(sessionPath, "info.csv");
        // create CSV file with header
        infoWriter = new StreamWriter(infoPath);
        infoWriter.WriteLine("ShotIndex,CamFOV,CamPositionX,CamPositionY,CamPositionZ,CamOrientationW,CamOrientationX,CamOrientationY,CamOrientationZ,TargetPositionX,TargetPositionY,TargetPositionZ,TargetOrientationW,CamOrientationX,CamOrientationY");
        
        StartCoroutine(GenerateLoop());
    }

    System.Collections.IEnumerator GenerateLoop()
    {
        while (shotIndex < NumImages)
        {
            RandomizeAll();
            yield return new WaitForSeconds(WaitForExposure);
            for (int i = 0; i < NumShotsPerExposure; i++)
            {
                SaveImages();
                RandomizeAllExceptSun();
                yield return new WaitForSeconds(0.5f);
            }
        }

        infoWriter.Close();
        enabled = false;
        Debug.Log($"Domain randomization session complete. Images and info saved to {sessionPath}");
    }
    
    void Update()
    {
        // gotta do this so the cameras adjust their auto-exposure all the time
        foreach(Camera cam in cams)
        {
            if (cam.targetTexture != null)
            {
                cam.Render();
            }
        }
    }


    public void RandomizeAll()
    {
        RandomizeSun();
        RandomizeSkyAndFog();
        RandomizeWaterSurface();
        RandomizeCameras();
        RandomizeNoiseMaterials();
        Debug.Log("Randomized all components for shot " + shotIndex);
    }

    public void RandomizeAllExceptSun()
    {
        RandomizeSkyAndFog();
        RandomizeWaterSurface();
        RandomizeCameras();
        RandomizeNoiseMaterials();
        Debug.Log("Randomized all components except sun for shot " + shotIndex);
    }

    void SaveImages()
    {
        // for each camera under the CamTF object, save the render target to a PNG file
        foreach (Camera cam in cams)
        {
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = cam.targetTexture;

            cam.Render();

            Texture2D image = new(cam.targetTexture.width, cam.targetTexture.height);
            image.ReadPixels(new Rect(0, 0, cam.targetTexture.width, cam.targetTexture.height), 0, 0);
            image.Apply();

            byte[] bytes = image.EncodeToPNG();
            File.WriteAllBytes($"{sessionPath}/{shotIndex}_{cam.name}.png", bytes);

            RenderTexture.active = currentRT;
        }
        infoWriter.Write($"{shotIndex},");

        infoWriter.Write($"{cams[0].fieldOfView},");

        var camTFENU = CamTF.transform.To<ENU>();
        infoWriter.Write($"{camTFENU.translation.x},");
        infoWriter.Write($"{camTFENU.translation.y},");
        infoWriter.Write($"{camTFENU.translation.z},");
        infoWriter.Write($"{camTFENU.rotation.w},");
        infoWriter.Write($"{camTFENU.rotation.x},");
        infoWriter.Write($"{camTFENU.rotation.y},");
        infoWriter.Write($"{camTFENU.rotation.z},");

        if (LookAtTarget != null)
        {
            var targetTFENU = LookAtTarget.transform.To<ENU>();
            infoWriter.Write($"{targetTFENU.translation.x},");
            infoWriter.Write($"{targetTFENU.translation.y},");
            infoWriter.Write($"{targetTFENU.translation.z},");
            infoWriter.Write($"{targetTFENU.rotation.w},");
            infoWriter.Write($"{targetTFENU.rotation.x},");
            infoWriter.Write($"{targetTFENU.rotation.y},");
            infoWriter.Write($"{targetTFENU.rotation.z},");
        }
        else
        {
            infoWriter.Write("-,");
            infoWriter.Write("-,");
            infoWriter.Write("-,");
            infoWriter.Write("-,");
            infoWriter.Write("-,");
            infoWriter.Write("-,");
            infoWriter.Write("-,"); 
        }
        infoWriter.WriteLine();
        shotIndex++;
        Debug.Log("Saved images and info for shot " + shotIndex);
    }

    public void RandomizeCameras()
    {
        if (CamTF == null) return;
        
        Vector3 positionOffset = Vector3.zero;
        // Randomize position
        if(CameraHorizontalPositionRange > 0f)
        {
            Vector3 randomHorizontalOffset = new(
                Random.Range(-CameraHorizontalPositionRange, CameraHorizontalPositionRange),
                0f,
                Random.Range(-CameraHorizontalPositionRange, CameraHorizontalPositionRange)
            );
            positionOffset += randomHorizontalOffset;
        }

        // Randomize vertical position
        if (CameraVerticalPositionRange > 0f)
        {
            Vector3 randomVerticalOffset = new(
                0f,
                Random.Range(-CameraVerticalPositionRange, CameraVerticalPositionRange),
                0f
            );
            positionOffset += randomVerticalOffset;
        }

        Vector3 centerPosition = transform.position;
        if (CenterCamerasHorizontallyOnTarget && LookAtTarget != null)
        {
            centerPosition = new(LookAtTarget.position.x, transform.position.y, LookAtTarget.position.z);
        }
        CamTF.position = centerPosition + positionOffset;

        // Randomize rotation
        if (CameraRotationRange > 0f)
        {
            Vector3 randomRotation = new(
                Random.Range(-CameraRotationRange, CameraRotationRange),
                Random.Range(-CameraRotationRange, CameraRotationRange),
                Random.Range(-CameraRotationRange, CameraRotationRange)
            );
            CamTF.localRotation = Quaternion.Euler(CamTF.localRotation.eulerAngles + randomRotation);
        }

        // Look at target if specified
        if (LookAtTarget != null)
        {
            Vector3 lookAt = LookAtTarget.position;
            // Apply random offset to look at target
            if (LookAtTargetOffsetHorizontalRange > 0f)
            {
                lookAt += new Vector3(
                    Random.Range(-LookAtTargetOffsetHorizontalRange, LookAtTargetOffsetHorizontalRange),
                    0f,
                    Random.Range(-LookAtTargetOffsetHorizontalRange, LookAtTargetOffsetHorizontalRange)
                );
            }
            if (LookAtTargetOffsetVerticalRange > 0f)
            {
                lookAt += new Vector3(
                    0f,
                    Random.Range(-LookAtTargetOffsetVerticalRange, LookAtTargetOffsetVerticalRange),
                    0f
                );
            }
            CamTF.LookAt(lookAt);
        }
        
        // Randomize FOV
        if (CameraFOVRange > 0f)
        {
            float randomFOVOffset = Random.Range(-CameraFOVRange, CameraFOVRange);
            foreach (Camera cam in cams)
            {
                cam.fieldOfView = CameraFOVBase + randomFOVOffset;
            }
        }
        
    }

    void RandomizeNoiseMaterials()
    {
        // YellowNoise group
        ApplyRandomDistractorAppearance(YellowNoiseRenderers, TargetYellowHueRange);

        // OrangeNoise group
        ApplyRandomDistractorAppearance(OrangeNoiseRenderers, TargetOrangeHueRange);
    }

    void ApplyRandomDistractorAppearance(Renderer[] renderers, Vector2 targetHueRange)
    {
        if (renderers == null || renderers.Length == 0) return;
        _mpb ??= new MaterialPropertyBlock();

        // Normalize user probabilities (sliders don't need to sum to 1)
        float hard = Mathf.Max(0f, HardNegativeChance);
        float warm = Mathf.Max(0f, WarmShiftedChance);
        float far  = Mathf.Max(0f, FarChance);

        float sum = hard + warm + far;
        if (sum < 0.0001f)
        {
            // Default
            hard = 0.20f;
            warm = 0.75f;
            far  = 0.05f;
            sum = 1f;
        }

        hard /= sum;
        warm /= sum;
        far  /= sum;

        float hardCut = hard;         // [0, hard)
        float warmCut = hard + warm;  // [hard, hard+warm)
        // far is the rest

        foreach (var rend in renderers)
        {
            if (rend == null) continue;

            // Per-renderer random choice (no groups)
            float mode = Random.value;

            float hueMin, hueMax;
            float satMin, satMax;
            float valMin, valMax;

            if (mode < hardCut)
            {
                // 1) Hard negatives: target like hues sometimes (color != class)
                hueMin = targetHueRange.x;
                hueMax = targetHueRange.y;

                satMin = 0.55f; satMax = 1.00f;
                valMin = 0.45f; valMax = 1.00f;
            }
            else if (mode < warmCut)
            {
                // 2) Warm-shifted: warm family but degraded (avoid to looks exactly like target")
                hueMin = 0.04f; hueMax = 0.23f;   // ~15°–83° (warm range)

                // Degrade appearance: lower saturation and/or value
                satMin = 0.08f; satMax = 0.65f;
                valMin = 0.10f; valMax = 0.80f;
            }
            else
            {
                // 3) Far colors: small portion to prevent warm==target
                if (Random.value < 0.5f)
                {
                    // Cool hues (blue/teal/purple)
                    hueMin = 0.53f; hueMax = 0.83f; // ~190°–300°
                    satMin = 0.20f; satMax = 1.00f;
                    valMin = 0.10f; valMax = 1.00f;
                }
                else
                {
                    // Neutrals (gray/black/white): low saturation
                    hueMin = 0.0f; hueMax = 1.0f;   // irrelevant when saturation low
                    satMin = 0.00f; satMax = 0.12f;
                    valMin = 0.06f; valMax = 0.95f;
                }
            }

            Color c = Random.ColorHSV(hueMin, hueMax, satMin, satMax, valMin, valMax, 1f, 1f);

            rend.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorID, c);

            if (RandomizeSurfaceAppearance)
            {
                // Avoid a lot of GUI options
                float strength = Mathf.Clamp01(SurfaceRandomizationStrength);

                // Keep metallic low (paint/plastic), but small variation
                float metallic = Random.Range(0.0f, 0.15f) * strength;

                // Big smoothness spread (useful in water scenes)
                // strength=0 -> ~0.10, strength=1 -> random [0.02..0.95]
                float smoothnessRand = Random.Range(0.02f, 0.95f);
                float smoothness = Mathf.Lerp(0.10f, smoothnessRand, strength);

                _mpb.SetFloat(MetallicID, metallic);
                _mpb.SetFloat(SmoothnessID, smoothness);
            }

            rend.SetPropertyBlock(_mpb);
        }
    }

    public void RandomizeSkyAndFog()
    {
        if (skyAndFogVolume == null) SetComponents();

        if (fog != null && FogDistanceRange > 0f)
        {
            float randomFogOffset = Random.Range(-FogDistanceRange, FogDistanceRange);
            fog.meanFreePath.value = Mathf.Max(1f, FogDistanceBase + randomFogOffset);
        }

        if (sky != null && AerosolDensityRange > 0f)
        {
            float randomAerosolOffset = Random.Range(-AerosolDensityRange, AerosolDensityRange);
            sky.aerosolDensity.value = Mathf.Max(0f, AerosolDensityBase + randomAerosolOffset);
        }
    }

    public void RandomizeSun()
    {
        if (sunLight == null || sunTF == null) SetComponents();

        if (sunTF != null)
        {
            // Randomize sun rotation
            if (SunRotationHorizontalRange > 0f || SunRotationVerticalRange > 0f)
            {
                float randomY = Random.Range(-SunRotationHorizontalRange, SunRotationHorizontalRange);
                float randomX = Random.Range(-SunRotationVerticalRange, SunRotationVerticalRange);
                float newX = SunRotationVerticalBase + randomX;
                newX = Mathf.Max(newX, 0f);
                float newY = SunRotationHorizontalBase + randomY;

                sunTF.rotation = Quaternion.Euler(newX, newY, 0f);
            }
        }

        if (sunLight != null)
        {
            if (SunEmissionTemperatureRange > 0f)
            {
                // Randomize sun temperature
                float randomTemperatureOffset = Random.Range(-SunEmissionTemperatureRange, SunEmissionTemperatureRange);
                sunLight.colorTemperature = SunEmissionTemperatureBase + randomTemperatureOffset;
            }

            if (SunIntensityThousandsRange > 0f)
            {
                // Randomize sun intensity
                float randomIntensityOffset = Random.Range(-SunIntensityThousandsRange * 1000f, SunIntensityThousandsRange * 1000f);
                sunLight.intensity = SunIntensityThousandsBase * 1000f + randomIntensityOffset;
            }
        }

    }

    public void RandomizeWaterSurface()
    {
        if (WaterSurface == null) SetComponents();
        if (WaterSurface == null) return;

        if (DistantWindSpeedRange > 0f)
        {
            float randomDistantWindSpeedOffset = Random.Range(-DistantWindSpeedRange, DistantWindSpeedRange);
            WaterSurface.largeWindSpeed = Mathf.Max(0f, DistantWindSpeedBase + randomDistantWindSpeedOffset);
        }

        if (LocalWindSpeedRange > 0f)
        {
            float randomLocalWindSpeedOffset = Random.Range(-LocalWindSpeedRange, LocalWindSpeedRange);
            WaterSurface.ripplesWindSpeed = Mathf.Max(0f, LocalWindSpeedBase + randomLocalWindSpeedOffset);
        }

        if (CurrentOrientationRange > 0f)
        {
            float randomCurrentOrientationOffset = Random.Range(-CurrentOrientationRange, CurrentOrientationRange);
            WaterSurface.largeOrientationValue = (CurrentOrientationBase + randomCurrentOrientationOffset) % 360f;
        }

        if (CurrentSpeedRange > 0f)
        {
            float randomCurrentSpeedOffset = Random.Range(-CurrentSpeedRange, CurrentSpeedRange);
            WaterSurface.largeCurrentSpeedValue = Mathf.Max(0f, CurrentSpeedBase + randomCurrentSpeedOffset);
        }

        if(RGBOcean)
        {
            float refR = Random.Range(0f, 1f);
            float refG = Random.Range(0f, 1f);
            float refB = Random.Range(0f, 1f);
            float a = Random.Range(0f, 1f);
            WaterSurface.refractionColor = new Color(refR, refG, refB, a);
            WaterSurface.scatteringColor = new Color(refR, refG, refB, a);
        }
    }
    


    void OnDrawGizmos()
    {
        if (CamTF != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = new Vector3(transform.position.x, transform.position.y, transform.position.z);
            if (LookAtTarget != null && CenterCamerasHorizontallyOnTarget)
            {
                center = new Vector3(LookAtTarget.position.x, transform.position.y, LookAtTarget.position.z);
            }
            Gizmos.DrawWireCube(center, new Vector3(
                CameraHorizontalPositionRange * 2f,
                CameraVerticalPositionRange * 2f,
                CameraHorizontalPositionRange * 2f
            ));
        }

        if (LookAtTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(LookAtTarget.position, new Vector3(
                LookAtTargetOffsetHorizontalRange * 2f,
                LookAtTargetOffsetVerticalRange * 2f,
                LookAtTargetOffsetHorizontalRange * 2f
            ));
        }


    }

}