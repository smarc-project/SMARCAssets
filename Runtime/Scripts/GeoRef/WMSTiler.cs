using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SmarcGUI;
using UnityEngine;
using UnityEngine.Networking;

namespace GeoRef
{

    [RequireComponent(typeof(GlobalReferencePoint))]
    public class WMSTiler : MonoBehaviour
    {
        [Header("WMS Tile Server")]
        string WMSUrl;
        string LayerName;

        [Header("Material Settings")]
        public Material TileMaterial;

        [Header("Area to load")]
        public float Radius = 100;

        [Header("Tile Settings")]
        public int TileSizePx = 256;
        public int TileSizeMeters = 50;
        [Tooltip("Max number of tiles to create. If the number of tiles exceeds this, no tiles will be created and a warning will be logged. Here to keep you from accidentally producing 10 million tiles.")]
        public int MaxNumTiles = 100;

        [Tooltip("Move all tiles north by this amount (in meters) to align satelite images with a reference point")]
        public float TileOffsetNorth = 0f;
        [Tooltip("Move all tiles east by this amount (in meters) to align satelite images with a reference point")]
        public float TileOffsetEast = 0f;

        GlobalReferencePoint refPt;

        // Set by the editor so MakeTiles can fetch textures outside play mode.
        public System.Func<IEnumerator, object> RunCoroutine;

        public void Awake()
        {
            refPt = GetComponent<GlobalReferencePoint>();
        }

        public void Start()
        {
            if (!LoadSettings()) return;
            MakeTiles();
        }

        public bool LoadSettings()
        {
            string settingsStoragePath = Path.Combine(GUIState.GetStoragePath(), "Settings");
            Directory.CreateDirectory(settingsStoragePath);
            string settingsFile = Path.Combine(settingsStoragePath, "WMSSettings.yaml");
            if (File.Exists(settingsFile))
            {
                var settings = File.ReadAllText(settingsFile);
                var deserializer = new YamlDotNet.Serialization.Deserializer();
                var settingsDict = deserializer.Deserialize<Dictionary<string, string>>(settings);
                if (settingsDict.ContainsKey("WMSUrl"))
                {
                    WMSUrl = settingsDict["WMSUrl"];
                    LayerName = settingsDict["LayerName"];
                }
            }
            else
            {
                Debug.LogWarning($"WMS settings file not found at {settingsFile}, creating dummy. Re-run the game to set the WMS URL and Layer Name.");
                var settingsDict = new Dictionary<string, string>
                {
                    { "WMSUrl", "" },
                    { "LayerName", "" }
                };
                var serializer = new YamlDotNet.Serialization.Serializer();
                var settingsYaml = serializer.Serialize(settingsDict);
                File.WriteAllText(settingsFile, settingsYaml);
                return false;
            }

            if (string.IsNullOrEmpty(WMSUrl) || string.IsNullOrEmpty(LayerName))
            {
                Debug.LogError($"WMS URL or Layer Name is not set. Please set them in {settingsFile}.");
                return false;
            }

            if (!WMSUrl.EndsWith("/"))
            {
                WMSUrl += "/";
            }
            return true;
        }

        public void ClearTiles()
        {
            var stale = new List<GameObject>();
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("Tile_")) stale.Add(child.gameObject);
            }
            foreach (var go in stale)
            {
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
        }

        public string MakeGetMapURL(double eastingMin, double northingMin, double eastingMax, double northingMax)
        {
            // Create the WMS URL
            var service = "WMS";
            var request = "GetMap";
            var styles = "";
            var format = "image/png";
            var transparent = "false";
            var version = "1.1.1";
            var map = LayerName;
            var width = TileSizePx;
            var height = TileSizePx;
            var srs = "EPSG:3857";
            var bboxStr = $"{eastingMin},{northingMin},{eastingMax},{northingMax}";
            // Create the URL
            string url = $"{WMSUrl}?service={service}&version={version}&request={request}&layers={map}&bbox={bboxStr}&width={width}&height={height}&srs={srs}&format={format}&styles={styles}&transparent={transparent}";

            return url;
        }

        IEnumerator RequestAndSetTile(GameObject quadObj, double eastingMin, double northingMin, double eastingMax, double northingMax)
        {
            string url = MakeGetMapURL(eastingMin, northingMin, eastingMax, northingMax);
            //Debug.Log($"Requesting WMS tile from URL: {url}");

            using UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(url);
            // Send the request and wait for a response
            yield return webRequest.SendWebRequest();
            Texture2D texture = null;

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error: {webRequest.error}. Trying again once in a second.");
                yield return new WaitForSeconds(1);
                // Try again once
                using UnityWebRequest webRequestRetry = UnityWebRequestTexture.GetTexture(url);
                yield return webRequestRetry.SendWebRequest();

                if (webRequestRetry.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Error again: {webRequestRetry.error}.");
                }
                else texture = DownloadHandlerTexture.GetContent(webRequestRetry);
            }
            else texture = DownloadHandlerTexture.GetContent(webRequest);

            if (texture != null)
            {
                // Create a mesh and assign the texture
                var meshFilter = quadObj.AddComponent<MeshFilter>();
                meshFilter.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

                var meshRenderer = quadObj.AddComponent<MeshRenderer>();
                // Create a new material instance for this tile
                Material tileMaterialInstance = new(TileMaterial)
                {
                    mainTexture = texture
                };
                meshRenderer.material = tileMaterialInstance;
                meshRenderer.receiveShadows = false;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            else
            {
                Debug.LogError($"Failed to load texture for tile {quadObj.name}. Destroying tile.");
                // Delete the tile GameObject if the request failed
                // check if edit mode or play mode, use the right destroy method
                if (Application.isPlaying) Destroy(quadObj);
                else DestroyImmediate(quadObj);
            }

        }

        public void MakeTiles()
        {
            // Split the area into tiles
            int numTiles = Mathf.CeilToInt(Radius * 2 / TileSizeMeters) + 1;
            Debug.Log($"Number of tiles: {numTiles * numTiles}");
            if(numTiles*numTiles > MaxNumTiles)
            {
                Debug.LogWarning($"Too many({numTiles * numTiles}) tiles to create. Please reduce the radius or increase tile size.");
                return;
            }

            if (refPt == null) refPt = GetComponent<GlobalReferencePoint>();
            ClearTiles();

            // Quads live in the scene's Unity frame (UTM or WebMercator).
            // WMS GetMap stays EPSG:3857: convert each Unity tile's corners to
            // lat/lon, then to Web Mercator. Adding TileSizeMeters onto 3857
            // easting as if 1 Unity m = 1 WM m is wrong in UTM (≈1.9× at 59°N)
            // and a no-op in WebMercator (identity). TileOffset* still shifts
            // the imagery in Web Mercator metres, not the quads.
            float half = TileSizeMeters / 2f;

            for (int x = 0; x < numTiles; x++)
            {
                for (int z = 0; z < numTiles; z++)
                {
                    var tileX = (x * TileSizeMeters) - Radius;
                    var tileZ = (z * TileSizeMeters) - Radius;

                    WebMercatorBBoxOfUnityTile(tileX, tileZ, half,
                        out var eastingMin, out var northingMin,
                        out var eastingMax, out var northingMax);
                    eastingMin += TileOffsetEast;
                    eastingMax += TileOffsetEast;
                    northingMin += TileOffsetNorth;
                    northingMax += TileOffsetNorth;

                    var tileName = $"Tile_{x}_{z}";

                    var quadObj = new GameObject(tileName);
                    quadObj.transform.SetParent(transform, false);
                    quadObj.transform.localPosition = new Vector3(tileX, 0, tileZ);
                    quadObj.transform.position += Vector3.up * 0.01f; // very slightly above the ground so it doesnt clip water
                    quadObj.transform.localScale = Vector3.one * TileSizeMeters;
                    quadObj.transform.rotation = Quaternion.Euler(90, 0, 0);

                    KickCoroutine(RequestAndSetTile(quadObj, eastingMin, northingMin, eastingMax, northingMax));
                }
            }
            
        }

        void KickCoroutine(IEnumerator routine)
        {
            if (RunCoroutine != null) RunCoroutine(routine);
            else StartCoroutine(routine);
        }

        void WebMercatorBBoxOfUnityTile(float tileX, float tileZ, float half,
            out double eastingMin, out double northingMin,
            out double eastingMax, out double northingMax)
        {
            var sw = transform.TransformPoint(new Vector3(tileX - half, 0, tileZ - half));
            var se = transform.TransformPoint(new Vector3(tileX + half, 0, tileZ - half));
            var nw = transform.TransformPoint(new Vector3(tileX - half, 0, tileZ + half));
            var ne = transform.TransformPoint(new Vector3(tileX + half, 0, tileZ + half));
            CornerToWebMercator(sw, out var e0, out var n0);
            CornerToWebMercator(se, out var e1, out var n1);
            CornerToWebMercator(nw, out var e2, out var n2);
            CornerToWebMercator(ne, out var e3, out var n3);
            eastingMin = System.Math.Min(System.Math.Min(e0, e1), System.Math.Min(e2, e3));
            eastingMax = System.Math.Max(System.Math.Max(e0, e1), System.Math.Max(e2, e3));
            northingMin = System.Math.Min(System.Math.Min(n0, n1), System.Math.Min(n2, n3));
            northingMax = System.Math.Max(System.Math.Max(n0, n1), System.Math.Max(n2, n3));
        }

        void CornerToWebMercator(Vector3 world, out double easting, out double northing)
        {
            var (lat, lon) = refPt.GetLatLonFromUnityXZ(world.x, world.z);
            (easting, northing) = refPt.GetWebMercatorFromLatLon(lat, lon);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, new Vector3(Radius * 2, 0.1f, Radius * 2));
        }


    }

}