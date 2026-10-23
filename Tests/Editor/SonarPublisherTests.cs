using System;
using System.Reflection;
using NUnit.Framework;
using RosMessageTypes.Sensor;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using VehicleComponents.Sensors;

public class SonarPublisherTests
{
    const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Type PublisherType = typeof(Sonar).Assembly.GetType("ROS.Publishers.SonarPointCloud_Pub");
    GameObject sensorObject;

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(sensorObject);

    Sonar CreateSensor(bool use3D)
    {
        sensorObject = new GameObject("Sonar publisher test");
        // Keep ROS OnEnable and model loading out of a message-format test.
        sensorObject.SetActive(false);
        return use3D ? sensorObject.AddComponent<Sonar3D>() : sensorObject.AddComponent<Sonar>();
    }

    (Component publisher, PointCloud2Msg message) CreatePublisher(Sonar sonar)
    {
        var publisher = sensorObject.AddComponent(PublisherType);
        var message = new PointCloud2Msg();
        PublisherType.BaseType.GetField("DataSource", InstanceMembers).SetValue(publisher, sonar);
        PublisherType.BaseType.BaseType.GetField("ROSMsg", InstanceMembers).SetValue(publisher, message);
        PublisherType.GetMethod("InitPublisher", InstanceMembers).Invoke(publisher, null);
        return (publisher, message);
    }

    static SonarHit Hit(Sonar sonar, Vector3 point, bool detected)
    {
        var result = new SonarHit(sonar);
        result.UpdateFromModel(new RaycastHit { point = point }, 0.5f, detected);
        return result;
    }

    static void UpdateMessage(Component publisher) =>
        PublisherType.GetMethod("UpdateMessage", InstanceMembers).Invoke(publisher, null);

    [Test]
    public void ExistingPublisherDiscovers3DWithoutAddingLegacySensor()
    {
        Sonar sonar = CreateSensor(true);
        CreatePublisher(sonar);
        Assert.That(sensorObject.GetComponent<Sonar>(), Is.SameAs(sonar));
        Assert.That(sensorObject.GetComponents<Sonar>(), Has.Length.EqualTo(1));
        Assert.That(sonar.TotalRayCount, Is.EqualTo(64 * 256));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PointCloudKeepsSchemaCoordinatesAndInvalidReturns(bool use3D)
    {
        Sonar sonar = CreateSensor(use3D);
        Vector3 point = new Vector3(1.25f, -2f, 3.5f);
        sonar.SonarHits = new[] { Hit(sonar, point, true), Hit(sonar, point, false) };
        var (publisher, message) = CreatePublisher(sonar);
        UpdateMessage(publisher);

        Assert.That(message.header.frame_id, Is.EqualTo("unity_origin"));
        Assert.That(message.height, Is.EqualTo(1));
        Assert.That(message.width, Is.EqualTo(2));
        Assert.That(message.point_step, Is.EqualTo(13));
        Assert.That(message.row_step, Is.EqualTo(26));
        Assert.That(message.data, Has.Length.EqualTo(26));
        Assert.That(message.is_dense, Is.False);
        Assert.That(message.is_bigendian, Is.False);
        Assert.That(Array.ConvertAll(message.fields, field => field.name), Is.EqualTo(new[] { "x", "y", "z", "intensity" }));
        Assert.That(Array.ConvertAll(message.fields, field => field.offset), Is.EqualTo(new uint[] { 0, 4, 8, 12 }));
        Assert.That(Array.ConvertAll(message.fields, field => field.datatype), Is.EqualTo(new[] { PointFieldMsg.FLOAT32, PointFieldMsg.FLOAT32, PointFieldMsg.FLOAT32, PointFieldMsg.UINT8 }));

        var expected = point.To<ENU>();
        Assert.That(BitConverter.ToSingle(message.data, 0), Is.EqualTo(expected.x));
        Assert.That(BitConverter.ToSingle(message.data, 4), Is.EqualTo(expected.y));
        Assert.That(BitConverter.ToSingle(message.data, 8), Is.EqualTo(expected.z));
        Assert.That(message.data[12], Is.EqualTo(127));
        Assert.That(float.IsNaN(BitConverter.ToSingle(message.data, 13)), Is.True);
        Assert.That(float.IsNaN(BitConverter.ToSingle(message.data, 17)), Is.True);
        Assert.That(float.IsNaN(BitConverter.ToSingle(message.data, 21)), Is.True);
        Assert.That(message.data[25], Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PublisherResizesForLatestScanAndReusesMatchingBuffer(bool use3D)
    {
        Sonar sonar = CreateSensor(use3D);
        var (publisher, message) = CreatePublisher(sonar);
        sonar.SonarHits = new[] { Hit(sonar, Vector3.one, true) };
        UpdateMessage(publisher);
        byte[] data = message.data;
        UpdateMessage(publisher);
        Assert.That(message.data, Is.SameAs(data));

        sonar.SonarHits = new[] { Hit(sonar, Vector3.one, true), Hit(sonar, Vector3.zero, false) };
        UpdateMessage(publisher);
        Assert.That(message.width, Is.EqualTo(2));
        Assert.That(message.data, Has.Length.EqualTo(26));

        sonar.SonarHits = Array.Empty<SonarHit>();
        UpdateMessage(publisher);
        Assert.That(message.width, Is.Zero);
        Assert.That(message.row_step, Is.Zero);
        Assert.That(message.data, Is.Empty);
    }
}
