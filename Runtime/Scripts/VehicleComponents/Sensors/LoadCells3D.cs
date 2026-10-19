using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VehicleComponents.Sensors
{

    /// <summary>
    /// A 3D load cell sensor that measures the force vector applied to a vehicle component.
    /// 
    /// Place on the Winch component of the vehicle, and set the ArticulationBody to the top rope link
    /// The sensor will calculate the force vector applied to the top rope link and convert it to load cell readings.
    /// </summary>
    [AddComponentMenu("Smarc/Sensor/LoadCells3D")]
    public class LoadCells3D : Sensor
    {
        [Header("LoadCells3D")]
        public ArticulationBody body;
        public int SmoothOverFramesNum = 1; // size of the queue
        public float LoadCellRadius = 0.2f; // radius of the load cells placement
        public float PlateWidth = 0.05f; // width of the plate connecting the load cells
        public bool DebugMode = false; // if true, draw the force vector
        public float DebugForceScale = 0.5f; // scales the length of the drawn force vector
        public float DebugLineThickness = 20f; // thickness of the drawn force vector in pixels

        [Header("Outputs")]
        public Vector3 LoadCellReadings; // readings of the individual load cells
        public Vector3 Force;
        public float Weight;


        private float3x3 equationMatrix; // matrix encoding the equation to convert the force vector to load cell readings
        private Queue<Vector3> forceReadings = new(); // Queue to store the last N force readings for moving average

        public override bool UpdateSensor(double deltaTime)
        {
            if (SmoothOverFramesNum < 1) {
                SmoothOverFramesNum = 1; // prevent division by zero
                Debug.LogWarning("SmoothOverFramesNum must be greater than 0. Setting to 1.");
            }
            Vector3 instantForce = new Vector3(0,0,0);
            float instantForceMagnitude = 0;
            Vector3 direction;
            if (body != null && body.transform.parent != null) // articulation body
            {
                ArticulationReducedSpace forces = body.driveForce;
                if (forces.dofCount == 1) instantForceMagnitude = forces[0];
                else Debug.LogWarning($"Insert the top rope link!");
                direction = body.transform.rotation * body.anchorRotation * -Vector3.right; // get the direction of the force in world space
                // -x because to get the direction of the force
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

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!DebugMode) return;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            Handles.color = Color.blue;
            Handles.DrawLine(transform.position, transform.position + transform.TransformDirection(Force) * DebugForceScale, DebugLineThickness);
        }
#endif

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
            equationMatrix = new float3x3(
                1, 1, 1,
                LoadCellRadius, LoadCellRadius*math.cos(math.PI/3*2), LoadCellRadius*math.cos(math.PI/3*4),
                0, LoadCellRadius*math.sin(math.PI/3*2), LoadCellRadius*math.sin(math.PI/3*4)
            );
            Vector3 b = new Vector3(-forceVector.y, -PlateWidth*forceVector.z, -PlateWidth*forceVector.x);
            return math.mul(math.inverse(equationMatrix), b);
        }

    }

}