using UnityEngine;
using RosMessageTypes.Sensor;
using Unity.Robotics.Core; //Clock

using CameraImageSensor = VehicleComponents.Sensors.CameraImage;
using ROS.Core;


namespace ROS.Publishers
{
    [RequireComponent(typeof(CameraImageSensor))]
    [RequireComponent(typeof(Camera))]
    class CameraInfo_Pub: ROSSensorPublisher<CameraInfoMsg, CameraImageSensor>
    {
        [Header("Camera Info")]
        [Header("Camera distortion model params for plumb_bob")]
        [Header("D")]
        public float k1=1;
        public float k2=1,t1=1,t2=1,k3=1;

        [Header("K")]
        public float fx=1;
        public float fy=1,cx=1,cy = 1;

        [Header("P")]
        public float fxp=1;
        public float fyp=1, cxp=1, cyp=1, Tx=1, Ty=1;
        Camera cam;


        protected override void InitPublisher()
        {
            ROSMsg.distortion_model = "plumb_bob";
            ROSMsg.D = new double[5];
            ROSMsg.K = new double[9];
            ROSMsg.P = new double[12];
            ROSMsg.height = (uint) DataSource.textureHeight;
            ROSMsg.width = (uint) DataSource.textureWidth;
            ROSMsg.header.frame_id = $"{robot_name}/{DataSource.linkName}";
            cam = GetComponent<Camera>();
        }

        protected override void UpdateMessage()
        {
            ROSMsg.header.stamp = new TimeStamp(Clock.time);   

            float[] D = {k1,k2,t1,t2,k3};
            
            // Camera intrinsic matrix K 
            fx = cam.focalLength * ROSMsg.width / cam.sensorSize.x;
            fy = cam.focalLength * ROSMsg.height / cam.sensorSize.y;
            cx = ROSMsg.width / 2f;
            cy = ROSMsg.height / 2f;
            
            float[] K = {
                fx, 0,  cx,
                0,  fy, cy,
                0,  0,  1
            };
            float[] P = {
                fxp, 0,   cxp, Tx,
                0,   fyp, cyp, Ty,
                0,   0,   1,   0
            };
            for(int i=0; i<5; i++)  ROSMsg.D[i] = D[i];
            for(int i=0; i<9; i++)  ROSMsg.K[i] = K[i];
            for(int i=0; i<12; i++) ROSMsg.P[i] = P[i];
        }
    }
}
