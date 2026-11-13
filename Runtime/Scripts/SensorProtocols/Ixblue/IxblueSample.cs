using Unity.Robotics.ROSTCPConnector.ROSGeometry;

namespace SensorProtocols.Ixblue
{

    /// <summary>
    /// Protocol-specific sample: position in degrees/metres; angular velocity
    /// in degrees/second; acceleration in m/s^2; velocity in m/s.
    /// ValidityTime is in 100-microsecond ticks since an arbitrary midnight.
    /// Quaternion fields are in StdBin wire order q0, q1, q2, q3.
    /// </summary>
    public struct IxblueSample
    {
        public double Latitude; /// [degs]
        public double Longitude; /// [degs]
        public float Altitude; /// [m]

        public float Heading; /// [degs]
        public float Roll; /// [degs]
        public float Pitch; /// [degs]

        public float Q0; /// Scalar first.
        public float Q1;
        public float Q2;
        public float Q3;

        public Vector3<FLU> AngularVelocity; /// [degs/sec]
        public Vector3<FLU> LinearAcceleration; /// [m/s^2]
        public Vector3<FLU> VesselVelocity; /// [m/s]

        public uint ValidityTime; /// [100s of microsecs]
    }
}
