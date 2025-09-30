using UnityEngine;

namespace VehicleComponents.Sensors
{
    [RequireComponent(typeof(Camera))]
    public class CameraImage: Sensor
    {
        [Header("Image")]
        public int textureWidth = 640;
        public int textureHeight = 480;
        public Texture2D image;

        [Header("Play mode preview")]
        public bool viewCam=true;
        public int viewX=100;
        public int viewY=30;
        public int viewHeight = 500;
        public int viewWidth = 500;

        RenderTexture renderedTexture;
        Camera cam;



        void Start()
        {
            renderedTexture = new RenderTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGB32);
            cam = GetComponent<Camera>();
            cam.targetTexture = renderedTexture;
            image = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false);
        }

        public override bool UpdateSensor(double deltaTime)
        {
            // If need be, use AsyncGPUReadback.RequestIntoNativeArray
            // for asynch render->texture movement
            // Check this for more: https://blog.unity.com/engine-platform/accessing-texture-data-efficiently
            // TODO: Found this as well. Might help with async impl. https://forum.unity.com/threads/getting-a-render-texture-byte-using-asyncgpureadback-request.1029679/
            
            // gotta read from the ARGB32 render into RGB24 (which is rgb8 in ros... THANK YOU.)
            RenderTexture.active = renderedTexture;
            image.ReadPixels(new Rect (0, 0, textureWidth, textureHeight), 0, 0, false);
            image.Apply(false, false);
            RenderTexture.active = null;
            return true;
        }

    }
}