using System;
using System.Net;
using System.Net.Sockets;
using Unity.Robotics.Core;
using UnityEngine;

/// Used for Clock.
using ROS.Core;

namespace SensorProtocols.Transport
{
    /// <summary>
    /// Scheduled publisher a-la-ROSPublisher. Owns network configuration and lifecycle.
    /// subclasses implement only CreatePacket. Contains a minimal set of functions
    /// for checking the validity of the configure endpoint and finding all
    /// active and inactive instances of the class.
    /// </summary>
    public abstract class UdpPublisher : MonoBehaviour
    {
        [Header("UDP Publisher")]
        public float frequency = 10f;

        [Header("Network")]
        public string destinationIp = "127.0.0.1";
        public int destinationPort = 8200;

        protected UdpTransport transport;
        private FrequencyTimer timer;
        private IPAddress resolvedAddress;

        public string DestinationIp => destinationIp;
        public int DestinationPort => destinationPort;
        public bool IsTransmitting => isActiveAndEnabled && transport != null;

        /// <summary>
        /// All UDP publishers in currently loaded scenes, including disabled
        /// components and those on inactive GameObjects. Prefab assets are excluded.
        /// </summary>
        public static UdpPublisher[] GetAllPublishers()
        {
            return FindObjectsByType<UdpPublisher>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        /// <summary>
        /// All components which are currently active and enabled.
        /// Use IsTransmitting to identify those whose socket is open.
        /// </summary>
        public static UdpPublisher[] GetActivePublishers()
        {
            return Array.FindAll(GetAllPublishers(),
                publisher => publisher != null && publisher.isActiveAndEnabled);
        }

        /// <summary>
        /// True when an already-initialized publisher sends to the same remote
        /// IP address and port. Compare normalized IPAddress values, not strings.
        /// </summary>
        private bool HasActiveEndpointConflict(IPAddress address, out UdpPublisher conflicting)
        {
            foreach (UdpPublisher other in GetAllPublishers())
            {
                if (other == null || other == this || !other.IsTransmitting)
                    continue;
                if (destinationPort == other.DestinationPort &&
                    address.Equals(other.resolvedAddress))
                {
                    conflicting = other;
                    return true;
                }
            }

            conflicting = null;
            return false;
        }

        private bool ValidateEndpoint(out IPAddress address)
        {
            // Validate IP address.
            if (!IPAddress.TryParse(destinationIp, out address) ||
                address.AddressFamily != AddressFamily.InterNetwork)
            {
                Debug.LogError(
                    $"[{name}] Invalid destination '{destinationIp}'. Enter a numeric IP address.",
                    this);
                return false;
            }

            // Validate UDP port.
            if (destinationPort < 1 || destinationPort > 65535)
            {
                Debug.LogError($"[{name}] Destination port must be 1..65535.", this);
                return false;
            }

            // Check for duplicate endpoints.
            if (HasActiveEndpointConflict(address, out UdpPublisher other))
            {
                Debug.LogError(
                    $"[{name}] Duplicate UDP destination {address}:{destinationPort}; " +
                    $"already used by '{other.name}'. Disabling this publisher.",
                    this);
                return false;
            }

            return true;
        }

        protected virtual void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!ValidateEndpoint(out IPAddress address))
            {
                enabled = false;
                return;
            }

            try
            {
                resolvedAddress = address;
                timer = new FrequencyTimer(frequency);
                InitPublisher();
                transport = new UdpTransport(address, destinationPort);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{name}] Unable to initialize UDP publisher: {ex}", this);
                transport?.Dispose();
                transport = null;
                enabled = false;
            }
        }

        /// <summary>
        /// Override this method to initialize the sensor or packet encodef.
        /// This method is called before sending.
        /// </summary>
        protected virtual void InitPublisher() { }

        /// <summary>
        /// Override this method to create/encode a packet for transmission.
        /// Called once per shcedules sned. Return null/empty to skip a packet.
        /// </summary>
        protected abstract byte[] CreatePacket(double simulationTime);

        /// <summary>
        /// Publish the message to ROS.
        /// We do this in FixedUpdate, so that things can be disabled and enabled at runtime.
        /// And not in Update, because usually FixedUpdate is called at a consistent rate and faster than frames.
        /// </summary>
        private void FixedUpdate()
        {
            if (transport == null || !timer.ExhaustTicks(Clock.Now))
                return;

            try
            {
                byte[] packet = CreatePacket(Clock.time);
                if (packet != null && packet.Length > 0)
                    transport.Send(packet);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{name}] UDP publish failed: {ex}. Publisher disabled.", this);
                enabled = false;
            }
        }

        protected virtual void OnDisable()
        {
            transport?.Dispose();
            transport = null;
            resolvedAddress = null;
        }

    }
}
