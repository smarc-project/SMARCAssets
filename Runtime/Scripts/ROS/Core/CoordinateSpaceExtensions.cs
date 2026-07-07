using RosMessageTypes.Geometry;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;

namespace Scripts.ROS.Core
{
    public static class CoordinateSpaceExtensions
    {
        public static Vector3 ToUnity<T>(this Vector3<T> vec) where T : ICoordinateSpace, new()
        {
            return new Vector3(vec.x, vec.y, vec.z);
        }

        public static Quaternion ToUnity<T>(this Quaternion<T> quat) where T : ICoordinateSpace, new()
        {
            return new Quaternion(quat.x, quat.y, quat.z, quat.w);
        }

        public static Vector3 To(this Vector3 self, CoordinateSpaceSelection coordinateSpaceSelection)
        {
            switch (coordinateSpaceSelection)
            {
                case CoordinateSpaceSelection.RUF:
                    return self.To<RUF>().ToUnity();
                case CoordinateSpaceSelection.FLU:
                    return self.To<FLU>().ToUnity();
                case CoordinateSpaceSelection.FRD:
                    return self.To<FRD>().ToUnity();
                case CoordinateSpaceSelection.ENU:
                    return self.To<ENU>().ToUnity();
                case CoordinateSpaceSelection.NED:
                    return self.To<NED>().ToUnity();
                case CoordinateSpaceSelection.ENULocal:
                    return self.To<ENULocal>().ToUnity();
                case CoordinateSpaceSelection.NEDLocal:
                    return self.To<NEDLocal>().ToUnity();
                default:
                    Debug.LogError("Invalid coordinate space " + coordinateSpaceSelection);
                    return self.To<RUF>().ToUnity();
            }
        }

        public static Quaternion To(this Quaternion self, CoordinateSpaceSelection coordinateSpaceSelection)
        {
            switch (coordinateSpaceSelection)
            {
                case CoordinateSpaceSelection.RUF:
                    return self.To<RUF>().ToUnity();
                case CoordinateSpaceSelection.FLU:
                    return self.To<FLU>().ToUnity();
                case CoordinateSpaceSelection.FRD:
                    return self.To<FRD>().ToUnity();
                case CoordinateSpaceSelection.ENU:
                    return self.To<ENU>().ToUnity();
                case CoordinateSpaceSelection.NED:
                    return self.To<NED>().ToUnity();
                case CoordinateSpaceSelection.ENULocal:
                    return self.To<ENULocal>().ToUnity();
                case CoordinateSpaceSelection.NEDLocal:
                    return self.To<NEDLocal>().ToUnity();
                default:
                    Debug.LogError("Invalid coordinate space " + coordinateSpaceSelection);
                    return self.To<RUF>().ToUnity();
            }
        }

        public static PointMsg ToROS(this Vector3 self, CoordinateSpaceSelection coordinateSpaceSelection)
        {
            switch (coordinateSpaceSelection)
            {
                case CoordinateSpaceSelection.RUF:
                    return self.To<RUF>();
                case CoordinateSpaceSelection.FLU:
                    return self.To<FLU>();
                case CoordinateSpaceSelection.FRD:
                    return self.To<FRD>();
                case CoordinateSpaceSelection.ENU:
                    return self.To<ENU>();
                case CoordinateSpaceSelection.NED:
                    return self.To<NED>();
                case CoordinateSpaceSelection.ENULocal:
                    return self.To<ENULocal>();
                case CoordinateSpaceSelection.NEDLocal:
                    return self.To<NEDLocal>();
                default:
                    Debug.LogError("Invalid coordinate space " + coordinateSpaceSelection);
                    return self.To<RUF>();
            }
        }

        public static QuaternionMsg ToROS(this Quaternion self, CoordinateSpaceSelection coordinateSpaceSelection)
        {
            switch (coordinateSpaceSelection)
            {
                case CoordinateSpaceSelection.RUF:
                    return self.To<RUF>();
                case CoordinateSpaceSelection.FLU:
                    return self.To<FLU>();
                case CoordinateSpaceSelection.FRD:
                    return self.To<FRD>();
                case CoordinateSpaceSelection.ENU:
                    return self.To<ENU>();
                case CoordinateSpaceSelection.NED:
                    return self.To<NED>();
                case CoordinateSpaceSelection.ENULocal:
                    return self.To<ENULocal>();
                case CoordinateSpaceSelection.NEDLocal:
                    return self.To<NEDLocal>();
                default:
                    Debug.LogError("Invalid coordinate space " + coordinateSpaceSelection);
                    return self.To<RUF>();
            }
        }
    }
}