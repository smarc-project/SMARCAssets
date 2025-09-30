using UnityEngine;
using Unity.Collections;          // NativeArray<T>
using RosMessageTypes.Sensor;
using Unity.Robotics.Core;        // Clock

using ROS.Core;
using CameraImageSensor = VehicleComponents.Sensors.CameraImage;

namespace ROS.Publishers
{
    [RequireComponent(typeof(CameraImageSensor))]
    class CameraImage_Pub : ROSSensorPublisher<ImageMsg, CameraImageSensor>
    {
        // Thank chat for this whole new version lol
        [Header("Flips")]
        [Tooltip("Flip top-to-bottom by default to match ROS image orientation.")]
        public bool flipVertically = true;

        [Tooltip("Flip left-to-right.")]
        public bool flipHorizontally = false;

        const int BYTES_PER_PIXEL = 3; // rgb8, source image is RGB24

        protected override void InitPublisher()
        {
            var h = DataSource.textureHeight;
            var w = DataSource.textureWidth;

            ROSMsg.data = new byte[h * w * BYTES_PER_PIXEL];
            ROSMsg.encoding = "rgb8";
            ROSMsg.height = (uint)h;
            ROSMsg.width  = (uint)w;
            ROSMsg.is_bigendian = 0;
            ROSMsg.step = (uint)(BYTES_PER_PIXEL * w);
            ROSMsg.header.frame_id = $"{robot_name}/{DataSource.linkName}";
        }

        protected override void UpdateMessage()
        {
            int h = (int)ROSMsg.height;
            int w = (int)ROSMsg.width;

            // 1) Bulk copy from Unity texture to ROS buffer (no per-byte loop)
            NativeArray<byte> src = DataSource.image.GetRawTextureData<byte>();
            if (ROSMsg.data == null || ROSMsg.data.Length != src.Length)
                ROSMsg.data = new byte[src.Length];

            src.CopyTo(ROSMsg.data); // fast memcpy-like copy

            // 2) Optional flips (done in-place, minimal extra allocs)
            if (flipVertically)
                FlipImageVerticallyInPlace(ROSMsg.data, w, h, BYTES_PER_PIXEL);

            if (flipHorizontally)
                FlipImageHorizontallyInPlace(ROSMsg.data, w, h, BYTES_PER_PIXEL);

            ROSMsg.header.stamp = new TimeStamp(Clock.time);
        }

        // --- Helpers ---

        // Swaps whole rows using a single temporary row buffer. O(h/2) row swaps.
        static void FlipImageVerticallyInPlace(byte[] buf, int w, int h, int bpp)
        {
            int rowSize = w * bpp;
            int half = h / 2;
            var tmp = new byte[rowSize];

            for (int y = 0; y < half; y++)
            {
                int top = y * rowSize;
                int bot = (h - 1 - y) * rowSize;

                System.Buffer.BlockCopy(buf, top, tmp, 0, rowSize);
                System.Buffer.BlockCopy(buf, bot, buf, top, rowSize);
                System.Buffer.BlockCopy(tmp, 0, buf, bot, rowSize);
            }
        }

        // Reverses pixels within each row (triplets for rgb8).
        // Uses swap of 3-byte groups; no per-byte copy loop over the whole image.
        static void FlipImageHorizontallyInPlace(byte[] buf, int w, int h, int bpp)
        {
            int rowSize = w * bpp;
            var tmpPix = new byte[bpp]; // small scratch for swapping two pixels

            for (int y = 0; y < h; y++)
            {
                int rowStart = y * rowSize;
                int left = 0;
                int right = (w - 1) * bpp;

                while (left < right)
                {
                    // swap pixel at 'left' with pixel at 'right'
                    System.Buffer.BlockCopy(buf, rowStart + left,  tmpPix, 0, bpp);
                    System.Buffer.BlockCopy(buf, rowStart + right, buf,   rowStart + left,  bpp);
                    System.Buffer.BlockCopy(tmpPix, 0,            buf,   rowStart + right, bpp);

                    left  += bpp;
                    right -= bpp;
                }
            }
        }
    }
}
