using System;
using System.Net;
using System.Net.Sockets;

namespace SensorProtocols.Transport
{
    /// <summary>
    /// Small UDP sender with no Unity or ROS dependencies.
    /// One instance sends datagrams to one configured destination that can be
    /// either unicast, broadcast, or multicast.
    /// </summary>
    public sealed class UdpTransport : IDisposable
    {
        private readonly UdpClient client;
        private readonly IPEndPoint destination;
        private bool disposed;

        public UdpTransport(IPAddress address, int port)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (address.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException(
                    "IPv4 destinations are supported.",
                    nameof(address));

            if (address.Equals(IPAddress.Any))
                throw new ArgumentException(
                    "Destination cannot be 0.0.0.0.",
                    nameof(address));

            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException(
                    nameof(port),
                    "Port must be 1..65535.");

            destination = new IPEndPoint(address, port);

            client = new UdpClient(AddressFamily.InterNetwork)
            {
                EnableBroadcast = true
            };
        }

        public void Send(byte[] datagram)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(UdpTransport));

            if (datagram == null)
                throw new ArgumentNullException(nameof(datagram));

            if (datagram.Length == 0)
                throw new ArgumentException(
                    "Cannot send an empty datagram.",
                    nameof(datagram));

            client.Send(datagram, datagram.Length, destination);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            client.Dispose();
        }
    }
}
