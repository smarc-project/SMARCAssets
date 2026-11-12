using UnityEngine;
using SensorProtocols.Transport;

public class UdpPublisherDiagnostics : MonoBehaviour
{
    [ContextMenu("List UDP Publishers")]
    public void ListPublishers()
    {
        UdpPublisher[] publishers =
            UdpPublisher.GetAllPublishers();

        Debug.Log(
            $"Found {publishers.Length} UDP publishers."
        );

        foreach (UdpPublisher publisher in publishers)
        {
            Debug.Log(
                $"Name: {publisher.name} | " +
                $"Endpoint: {publisher.DestinationIp}:" +
                $"{publisher.DestinationPort} | " +
                $"Enabled: {publisher.isActiveAndEnabled} | " +
                $"Transmitting: {publisher.IsTransmitting}"
            );
        }
    }
}
