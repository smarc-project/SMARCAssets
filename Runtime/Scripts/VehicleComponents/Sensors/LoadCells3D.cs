using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace VehicleComponents.Sensors
{

    [AddComponentMenu("Smarc/Sensor/LoadCells3D")]
    public class LoadCells3D : Sensor
    {
        [Header("LoadCells3D")]
        private Queue<Vector3> forceReadings = new(); // Queue to store the last N force readings for moving average
        public int SmoothOverFramesNum = 5; // size of the queue
        public Vector3 LoadCellReadings; // readings of the individual load cells
        public Vector3 Force;
        public float Weight;
        // public Joint joint; // for now, allow only articulation bodies
        public ArticulationBody body;

        public float TriangleSize = 0.2f; // size of the triangle for load cell placement
        public float TriangleHeight = 0.05f; // height of the triangle for load cell placement

        private float3x3 equationMatrix; // matrix encoding the equation to convert the force vector to load cell readings

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
                direction = body.transform.rotation * body.anchorRotation * -Vector3.right; // get the direction of the force in world space
                // -x because to get the dirrection of the force
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
            LoadCellReadings = GetLoadCellReadings(Force);
            Weight = Force.magnitude / Physics.gravity.magnitude;
            return true;
        }

        /// <summary>
        /// Calculates the reading of individual load cells from the force vector.
        /// </summary>
        /// <param name="forceVector">The force vector to calculate load cell readings from.</param>
        /// <returns>The load cell readings.</returns>
        private Vector3 GetLoadCellReadings(Vector3 forceVector)
        {
            // populate the equation matrix
            // [1   1  1]   [f1]   [-Fy]
            // [z1 z2 z3] * [f2] = [-h*Fz]
            // [x1 x2 x3]   [f3]   [-h*Fx]
            equationMatrix = new float3x3( // created by columns
                1, 1, 1,
                TriangleSize, TriangleSize*math.cos(math.PI/3*2), TriangleSize*math.cos(math.PI/3*4),
                0, TriangleSize*math.sin(math.PI/3*2), TriangleSize*math.sin(math.PI/3*4)
            );
            Vector3 b = new Vector3(-forceVector.y, -TriangleHeight*forceVector.z, -TriangleHeight*forceVector.x);
            return math.mul(math.inverse(equationMatrix), b);
        }

    }

}