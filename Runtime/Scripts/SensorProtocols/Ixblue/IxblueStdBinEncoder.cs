using System;
using System.IO;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;

namespace SensorProtocols.Ixblue
{
    /// <summary>
    /// Encoder for iXblue StdBin version 3 navigation packets.
    /// Navigation blocks: 0, 5, 7, 15, 16, 17, 22, 26, 28.
    /// Intentionally excludes the system date: configure the target ROS driver
    /// with time_source: ros (no /clock required if use_sim_time is false).
    /// </summary>
    public sealed class IxblueStdBinEncoder
    {
        public const byte ProtocolVersion = 3;

        public const uint NavigationBitmask =
            (1u << 0) |   // Attitude / heading, roll, pitch
            (1u << 5) |   // Angular velocity in vessel axes
            (1u << 7) |   // Position
            (1u << 15) |  // INS algorithm status
            (1u << 16) |  // INS system status
            (1u << 17) |  // INS user status
            (1u << 22) |  // Velocity in vessel axes
            (1u << 26) |  // Quaternion
            (1u << 28);   // Raw acceleration

        public const int TelegramSize = 146;

        /// <summary>
        /// Encodes a complete datagram (magic/header + selected blocks + checksum).
        /// All input values are already in StdBin units and reference frames.
        /// </summary>
        public byte[] Encode(IxblueSample sample, uint frameCounter)
        {
            using (var stream = new MemoryStream(TelegramSize))
            using (var writer = new BinaryWriter(stream))
            {
                WriteHeader(writer, sample, frameCounter);

                // Blocks MUST be serialized in ascending bit index order.
                WriteNavigationData(writer, sample);

                // StdBin telegram size includes its four-byte checksum.
                if (stream.Length + 4 != TelegramSize)
                    throw new InvalidOperationException(
                        "StdBin telegram layout does not match declared size.");

                writer.Flush();
                byte[] bytesBeforeChecksum = stream.ToArray();

                uint checksum = 0u;
                foreach (byte value in bytesBeforeChecksum)
                    checksum = unchecked(checksum + value);

                WriteUInt32BE(writer, checksum);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void WriteHeader(
            BinaryWriter writer, IxblueSample sample, uint frameCounter)
        {
            writer.Write((byte)'I');
            writer.Write((byte)'X');
            writer.Write(ProtocolVersion);

            WriteUInt32BE(writer, NavigationBitmask);
            WriteUInt32BE(writer, 0u); // Extended navigation mask
            WriteUInt32BE(writer, 0u); // External sensor mask
            WriteUInt16BE(writer, TelegramSize);

            WriteUInt32BE(writer, sample.ValidityTime);
            WriteUInt32BE(writer, frameCounter);
        }

        private static void WriteNavigationData(BinaryWriter writer, IxblueSample sample)
        {
            // Bit 0 - Attitude
            WriteFloatBE(writer, sample.Heading);
            WriteFloatBE(writer, sample.Roll);
            WriteFloatBE(writer, sample.Pitch);

            // Bit 5 - Vessel angular velocity (degrees / second)
            WriteVector3BE(writer, sample.AngularVelocity);

            // Bit 7 - Position (latitude, longitude, alt reference, altitude)
            WriteDoubleBE(writer, sample.Latitude);
            WriteDoubleBE(writer, sample.Longitude);
            writer.Write((byte)0); // Synthetic datum, geoid; SMARC alt is scene Y
            WriteFloatBE(writer, sample.Altitude);

            // Bits 15-17 - Fixed nominal statuses
            WriteNominalStatus(writer);

            // Bit 22 - Vessel-frame velocity
            WriteVector3BE(writer, sample.VesselVelocity);

            // Bit 26 - Quaternion q0, q1, q2, q3 (wire convention)
            WriteFloatBE(writer, sample.Q0);
            WriteFloatBE(writer, sample.Q1);
            WriteFloatBE(writer, sample.Q2);
            WriteFloatBE(writer, sample.Q3);

            // Bit 28 - Raw acceleration (m / s^2)
            WriteVector3BE(writer, sample.LinearAcceleration);
        }

        private static void WriteNominalStatus(BinaryWriter writer)
        {
            // Bit 15: 4 x UInt32. Algorithm word 1 bit 0 = NAVIGATION.
            WriteUInt32BE(writer, 1u);
            WriteUInt32BE(writer, 0u);
            WriteUInt32BE(writer, 0u);
            WriteUInt32BE(writer, 0u);

            // Bit 16: 3 x UInt32. All fault flags clear;
            // critically, system word 2 WAIT_FOR_POSITION bit is unset.
            WriteUInt32BE(writer, 0u);
            WriteUInt32BE(writer, 0u);
            WriteUInt32BE(writer, 0u);

            // Bit 17: INS user status, synthetic placeholder.
            WriteUInt32BE(writer, 0u);
        }

        private static void WriteUInt16BE(BinaryWriter writer, ushort value)
        {
            writer.Write((byte)(value >> 8));
            writer.Write((byte)value);
        }

        private static void WriteUInt32BE(BinaryWriter writer, uint value)
        {
            writer.Write((byte)(value >> 24));
            writer.Write((byte)(value >> 16));
            writer.Write((byte)(value >> 8));
            writer.Write((byte)value);
        }

        private static void WriteFloatBE(BinaryWriter writer, float value)
        {
            WriteBytesBE(writer, BitConverter.GetBytes(value));
        }

        private static void WriteDoubleBE(BinaryWriter writer, double value)
        {
            WriteBytesBE(writer, BitConverter.GetBytes(value));
        }

        private static void WriteVector3BE(BinaryWriter writer, Vector3<FLU> value)
        {
            WriteFloatBE(writer, value.x);
            WriteFloatBE(writer, value.y);
            WriteFloatBE(writer, value.z);
        }

        private static void WriteBytesBE(BinaryWriter writer, byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            writer.Write(bytes);
        }
    }
}
