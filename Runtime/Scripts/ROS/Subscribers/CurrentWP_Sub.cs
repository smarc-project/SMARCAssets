using UnityEngine;

using RosMessageTypes.SmarcMission; // GotoWaypointMsg
using GeoRef; // GPSRef

using ROS.Core;


namespace ROS.Subscribers
{

    public enum PointingType
    {
        Position,
        LineRenderer
    }

    public class CurrentWP_Sub : ROSBehaviour
    {
        GlobalReferencePoint globalRef;

        [Tooltip("Position: Teleport this object itself to where the WP is. LineRenderer: Draw some lines to represent the WP, fancier.")]
        public PointingType pointingType;

        LineRenderer surfacePointer, circle;


        protected override void StartROS()
        {
            globalRef = FindFirstObjectByType<GlobalReferencePoint>();
            if(pointingType == PointingType.LineRenderer)
            {
                surfacePointer = transform.Find("SurfacePointer").GetComponent<LineRenderer>();
                circle = transform.Find("Circle").GetComponent<LineRenderer>();
            }

            rosCon.Subscribe<GotoWaypointMsg>(topic, UpdateMessage);
        }

        void UpdatePosition(GotoWaypointMsg msg)
        {
            var position = new Vector3(0,0,0);
            (position.x, position.z) = globalRef.GetUnityXZFromLatLon(msg.lat, msg.lon);
            position.y = (float) -msg.travel_depth;

            transform.position = position;
        }

        void UpdateLines(GotoWaypointMsg msg)
        {
            var center = new Vector3(0,0,0);
            (center.x, center.z) = globalRef.GetUnityXZFromLatLon(msg.lat, msg.lon);
            center.y = (float) -msg.travel_depth;

            int numPts = 50;
            float circleRad = (float)msg.goal_tolerance;
            var circlePoints = new Vector3[numPts];
            for(int i=0; i<numPts; i++)
            {
                float rad = i * 2*Mathf.PI / (numPts-1);
                var x = center.x + circleRad * Mathf.Cos(rad);
                var z = center.z + circleRad * Mathf.Sin(rad);
                circlePoints[i] = new Vector3(x, center.y, z);
            }
            circle.positionCount = circlePoints.Length;
            circle.SetPositions(circlePoints);

            var pointerPoints = new Vector3[3];
            pointerPoints[0] = new Vector3(center.x, 2, center.z);
            pointerPoints[1] = center;
            pointerPoints[2] = circlePoints[0];
            surfacePointer.positionCount = pointerPoints.Length;
            surfacePointer.SetPositions(pointerPoints);

        }

        void UpdateMessage(GotoWaypointMsg msg)
        {
            // the bt publishes this with UTM and lat/lon in it
            // lat lon is probably easier to handle in unity than utm
            // since we have a gps reference point that does the lat/lon -> unity world
            // conversion already
            if(globalRef == null)
            {
                Debug.Log($"[{transform.name}] No GlobalReferencePoint found! Disabling.");
                enabled = false;
                rosCon.Unsubscribe(topic);
                return;
            }

            switch(pointingType)
            {
                case PointingType.Position: UpdatePosition(msg); break;
                case PointingType.LineRenderer: UpdateLines(msg); break;
            }

            

        }
    }
}
