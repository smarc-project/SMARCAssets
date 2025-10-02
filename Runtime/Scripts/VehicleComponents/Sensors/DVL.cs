using UnityEngine;

namespace VehicleComponents.Sensors
{
    public class DVL: Sensor
    {
        [Header("DVL")]
        public int numBeams = 4;
        public int minHitsToReport = 3;
        public float maxRange = 50f;
        public float minRange = 0.05f;
        public float angleFromVertical = 22.5f;
        public float rotationOffset = 135f;
        public float verticalEmitOffset = -0.01f;
        public bool invertBeamOrder = true;
        public bool drawBeams = true;

        [Header("Current values")]
        public bool bottomLock;
        public Vector3 velocity;
        public float altitude;
        public float[] ranges;
        public int numHits;

        void Start()
        {
            ranges = new float[numBeams];
        }

        public override bool UpdateSensor(double deltaTime)
        {
            // Base directions depending on the pose of the DVL
            Vector3 right = transform.TransformDirection(Vector3.right);
            Vector3 down = transform.TransformDirection(-Vector3.up);
            Vector3 source = transform.position + transform.TransformDirection(Vector3.up)*verticalEmitOffset;
            // Start looking down
            Vector3 direction = transform.TransformDirection(-Vector3.up);
            // Tilt forward first
            direction = Quaternion.AngleAxis(angleFromVertical, right) * direction;
            // Rotate around vertical for the offset
            direction = Quaternion.AngleAxis(rotationOffset, down) * direction;

            var angleAroundVertical = 360/numBeams;
            if(invertBeamOrder) angleAroundVertical*=-1;

            numHits = 0;
            for(int i=0;i < numBeams; i++)
            {
                // Then rotate around vertical each beam
                // according to their index
                direction = Quaternion.AngleAxis(angleAroundVertical, down) * direction;
                // draw the first 4 beams with colors getting hotter
                if(drawBeams)
                {
                    Color c = Color.Lerp(Color.red, Color.green, (i+1)/(float)numBeams);
                    Debug.DrawLine(source, source + direction, c, 0.5f);
                }
                RaycastHit beamHit;
                if(Physics.Raycast(source, direction, out beamHit, maxRange))
                {
                    if(beamHit.distance >= minRange)
                    {
                        // finally, its a valid hit
                        if(drawBeams) Debug.DrawLine(source, beamHit.point, Color.yellow, 0.5f);
                        ranges[i] = beamHit.distance;
                        numHits++;
                    }
                }
            }

            bottomLock = numHits >= minHitsToReport;
            // If not enough hits, no velocity or altitude or anything...
            if(!bottomLock) return false;

            velocity = mixedBody.transform.InverseTransformVector(mixedBody.velocity);
            
            // Altitude is a little trickier since we're faking it
            // rather than doing the whole beams thing...
            // So instead, we just do a raycast straight down
            // and use that as our altitude
            // This should be about the same as getting beam distances, their angles and calcing
            // the distance a "straight down" beam would produce from those.
            RaycastHit altHit;
            if(Physics.Raycast(source, -Vector3.up, out altHit, maxRange))
            {
                altitude = altHit.distance;
            }
            
            return true;

        }

    }
}