using System.Collections;
using Force;
using GeoRef;
using SmarcGUI.MissionPlanning.Params;
using UnityEngine;


namespace SmarcGUI.Connections
{
    [RequireComponent(typeof(WaspHeartbeat))]
    public class WaspBasics : MQTTPublisher
    {
        public Rigidbody robotRB;
        public ArticulationBody robotAB;

        MixedBody body;

        public float Rate = 5.0f;
        WaspHeartbeat waspHeartbeat;
        GlobalReferencePoint globalReferencePoint;

        GeoPoint posGP;

        void Awake()
        {
            waspHeartbeat = GetComponent<WaspHeartbeat>();
            mqttClient = FindFirstObjectByType<MQTTClientGUI>();
            body = new MixedBody(robotAB, robotRB);
            if(!body.isValid)
            {
                Debug.LogError("No valid body found for WaspBasics, disabling!");
                gameObject.SetActive(false);
                return;
            }

            globalReferencePoint = FindFirstObjectByType<GlobalReferencePoint>();
            if(globalReferencePoint == null)
            {
                Debug.LogError("GlobalReferencePoint not found, WaspBasics will not work, destroying it");
                gameObject.SetActive(false);
                return;
            }

            posGP = new GeoPoint();
        }

        public override void StartPublishing()
        {
            if(!body.isValid) return;
            publish = true;
            StartCoroutine(PublishCoroutine());
        }

        public override void StopPublishing()
        {
            publish = false;
        }

        IEnumerator PublishCoroutine()
        {
            var wait = new WaitForSeconds(1.0f / Rate);
            while (publish)
            {
                if(!waspHeartbeat.HasPublihed) yield return wait;
                var (lat, lon) = globalReferencePoint.GetLatLonFromUnityXZ(body.position.x, body.position.z);
                posGP.latitude = lat;
                posGP.longitude = lon;
                posGP.altitude = body.position.y;
                mqttClient.Publish(waspHeartbeat.TopicBase+"sensor/position", posGP.ToJson());

                // Unity's axes are already correct for heading :)
                float heading = body.rotation.eulerAngles.y;
                mqttClient.Publish(waspHeartbeat.TopicBase+"sensor/heading", heading.ToString());

                float course = body.velocity.sqrMagnitude > 0.05*0.05 ? Mathf.Atan2(body.velocity.x, body.velocity.z) * Mathf.Rad2Deg : heading;
                mqttClient.Publish(waspHeartbeat.TopicBase+"sensor/course", course.ToString());

                float speed = body.velocity.magnitude;
                mqttClient.Publish(waspHeartbeat.TopicBase+"sensor/speed", speed.ToString());
                
                yield return wait;
            }
        }

    }
}