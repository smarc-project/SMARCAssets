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

        [Tooltip("Move all tiles north by this amount (in meters) to align satelite images with a reference point")]
        public float TileOffsetNorth = 0f;
        [Tooltip("Move all tiles east by this amount (in meters) to align satelite images with a reference point")]
        public float TileOffsetEast = 0f;

        GlobalReferencePoint refPt;

        public void Awake()
        {
            refPt = GetComponent<GlobalReferencePoint>();
        }

        public void Start()
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
                return;
            }

            if (string.IsNullOrEmpty(WMSUrl) || string.IsNullOrEmpty(LayerName))
            {
                Debug.LogError($"WMS URL or Layer Name is not set. Please set them in {settingsFile}.");
                return;
            }

            if (!WMSUrl.EndsWith("/"))
            {
                WMSUrl += "/";
            }

            // Make the tiles
            MakeTiles();
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

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error: {webRequest.error}");
            }
            else
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);
                // Create a mesh and assign the texture
                var meshFilter = quadObj.AddComponent<MeshFilter>();
                meshFilter.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

                var meshRenderer = quadObj.AddComponent<MeshRenderer>();
                // This is for play mode! If you want to use this in editor, then you need to _create_ a new material for each
                // tile, and save it as a new asset, then assign it. Otherwise the instance of the material dangles around and is not saved.
                // In game mode, this is fine, as the material is created in memory and assigned to the mesh renderer.
                meshRenderer.material = TileMaterial;
                meshRenderer.material.mainTexture = texture;
                meshRenderer.receiveShadows = false;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        public void MakeTiles()
        {
            // Split the area into tiles
            int numTiles = Mathf.CeilToInt(Radius * 2 / TileSizeMeters) + 1;
            Debug.Log($"Number of tiles: {numTiles * numTiles}");
            if(numTiles*numTiles > 100)
            {
                Debug.LogWarning("Too many tiles to create. Please reduce the radius or  increase tile size.");
                return;
            }

            // Get the WebMercator coordinates of the reference point
            if (refPt == null) refPt = GetComponent<GlobalReferencePoint>();
            var (refEasting, refNorthing) = refPt.GetWebMercatorFromLatLon(refPt.Lat, refPt.Lon);
            // Adjust the reference point by the tile offsets
            refEasting += TileOffsetEast;
            refNorthing += TileOffsetNorth;

            for (int x = 0; x < numTiles; x++)
            {
                for (int z = 0; z < numTiles; z++)
                {
                    // Calculate the position of the tiles center
                    var tileX = (x * TileSizeMeters) - Radius;
                    var tileZ = (z * TileSizeMeters) - Radius;

                    // Calculate the bounding box of the tile in WebMercator coordinates
                    var tileEasting = refEasting + tileX;
                    var tileNorthing = refNorthing + tileZ;
                    // Calculate the min and max coordinates of the tile
                    // in WebMercator coordinates
                    var eastingMin = tileEasting - TileSizeMeters / 2;
                    var northingMin = tileNorthing - TileSizeMeters / 2;
                    var eastingMax = tileEasting + TileSizeMeters / 2;
                    var northingMax = tileNorthing + TileSizeMeters / 2;

                    // Create a new GameObject for the tile
                    var tileName = $"Tile_{x}_{z}";
                    var quadObj = new GameObject(tileName);
                    quadObj.transform.SetParent(transform, false);
                    quadObj.transform.localPosition = new Vector3((float)tileX, 0, (float)tileZ);
                    quadObj.transform.position += Vector3.up * 0.01f; // very slightly above the ground so it doesnt clip water
                    quadObj.transform.localScale = Vector3.one * TileSizeMeters;
                    quadObj.transform.rotation = Quaternion.Euler(90, 0, 0);

                    // Request the tile texture
                    StartCoroutine(RequestAndSetTile(quadObj, eastingMin, northingMin, eastingMax, northingMax));
                }
            }
            
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, new Vector3(Radius * 2, 0.1f, Radius * 2));
        }


    }

}