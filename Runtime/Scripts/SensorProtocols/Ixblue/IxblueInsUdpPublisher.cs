using System;
using SensorProtocols.Transport;
using UnityEngine;

namespace SensorProtocols.Ixblue
{
    [AddComponentMenu("iXblue INS UDP Publisher")]
    [RequireComponent(typeof(IxblueMeasurementAdapter))]
    public class IxblueInsUdpPublisher : UdpPublisher
    {
        private IxblueMeasurementAdapter adapter;
        private IxblueStdBinEncoder encoder;
        private uint frameCounter;

        protected override void InitPublisher()
        {
            adapter = GetComponent<IxblueMeasurementAdapter>();
            if (adapter == null || !adapter.HasSources)
                throw new InvalidOperationException(
                    "iXblue adapter requires assigned IMU and GPS components.");

            encoder = new IxblueStdBinEncoder();
            frameCounter = 0;
        }

        protected override byte[] CreatePacket(double simulationTime)
        {
            IxblueSample sample = adapter.GetSample();

            // Clock.time is elapsed simulated time, not an absolute UTC epoch.
            // Target driver must use time_source=ros.
            double secondsOfDay = simulationTime % 86400.0;
            if (secondsOfDay < 0)
                secondsOfDay += 86400.0;

            sample.ValidityTime = (uint)(secondsOfDay * 10000.0);

            return encoder.Encode(sample, frameCounter++);
        }
    }
}
