using System.Collections.Generic;
using UnityEngine;

namespace VehicleComponents.Sensors
{

    [AddComponentMenu("Smarc/Sensor/LoadCells3D")]
    public class LoadCells3D : Sensor
    {
        [Header("LoadCells3D")]
        private Queue<Vector3> forceReadings = new(); // Queue to store the last N force readings for moving average
        public int SmoothOverFramesNum = 5; // size of the queue
        public Vector3 Force;
        public float Weight;
        // public Joint joint; // for now, allow only articulation bodies
        public ArticulationBody body;


        public override bool UpdateSensor(double deltaTime)
        {
            if (SmoothOverFramesNum <= 0) {
                SmoothOverFramesNum = 1; // prevent division by zero
                Debug.LogWarning("SmoothOverFramesNum must be greater than 0. Setting to 1.");
            }
            Vector3 instantForce = new Vector3(0,0,0);
            float instantForceMagnitude = 0;
            Vector3 direction = new Vector3(0,0,0);
            if (body != null && body.transform.parent != null) // articulation body
            {
                ArticulationReducedSpace forces = body.driveForce;
                if (forces.dofCount == 1) instantForceMagnitude = forces[0];
                else Debug.LogWarning($"Insert the top rope link!");
                direction = body.transform.rotation * body.anchorRotation * Vector3.right; // get the direction of the force in world space
                direction = transform.InverseTransformDirection(direction); // convert to local space
                instantForce = direction * instantForceMagnitude;
            }

            forceReadings.Enqueue(instantForce);
            while (forceReadings.Count > SmoothOverFramesNum) forceReadings.Dequeue(); // remove oldest readings

            // Calculate the moving average
            Vector3 sum = new Vector3(0,0,0);
            foreach (Vector3 f in forceReadings)
            {
                sum += f;
            }
            Force = sum / forceReadings.Count;
            return true;
        }

    }

}