using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Robotics.UrdfImporter;
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
        public Transform loadCell1;
        public Transform loadCell2;
        public Transform loadCell3;
        [SerializeField, Min(1)]
        private int SmoothOverFramesNum = 1; // size of the queue
        [SerializeField]
        private float LoadCellRadius = 0.2f; // radius of the load cells placement
        [SerializeField]
        private float PlateWidth = 0.05f; // width of the plate connecting the load cells
        private bool recomputeEquationMtx = true; 
        [SerializeField]
        private bool DebugMode = false; // if true, draw the force vector
        [SerializeField]
        private float DebugForceScale = 0.5f; // scales the length of the drawn force vector
        [SerializeField]
        private float DebugLineThickness = 10f; // thickness of the drawn force vector in pixels

        [Header("Outputs")]
        public Vector3 LoadCellReadings; // readings of the individual load cells
        public Vector3 Force;

        private float3x3 equationMatrix; // matrix encoding the equation to convert the force vector to load cell readings
        private Queue<Vector3> forceReadings = new(); // Queue to store the last N force readings for moving average


        void Start()
        {
            if (body == null || body.transform.parent == null)
            {
                Debug.LogWarning($"[LoadCells3D] ArticulationBody not set or has no parent. Disabling sensor.");
                enabled=false;
                return;
            }
            ArticulationReducedSpace forces = body.driveForce;
            if (forces.dofCount != 1)
            {
                Debug.LogWarning($"[LoadCells3D] ArticulationBody has {forces.dofCount} DOFs. LoadCells3D only supports 1 DOF prismatic joints. Disabling sensor.");
                enabled=false;
                return;
            }
            if (loadCell1 == null || loadCell2 == null || loadCell3 == null)
            {
                Debug.LogWarning($"[LoadCells3D] One or more load cell ArticulationBodies not set. Disabling sensor.");
                enabled=false;
                return;
            }
        } 

        void OnValidate()
        {
            // reposition the load cells
            if (loadCell1 != null && loadCell2 != null && loadCell3 != null)
            {
                loadCell1.transform.localPosition = new Vector3(0, 0, LoadCellRadius);
                loadCell2.transform.localPosition = new Vector3(LoadCellRadius * Mathf.Sin(Mathf.PI / 3 * 2), 0, LoadCellRadius * Mathf.Cos(Mathf.PI / 3 * 2));
                loadCell3.transform.localPosition = new Vector3(LoadCellRadius * Mathf.Sin(Mathf.PI / 3 * 4), 0, LoadCellRadius * Mathf.Cos(Mathf.PI / 3 * 4));
            }
            recomputeEquationMtx = true;
        }


        public override bool UpdateSensor(double deltaTime)
        {
            Vector3 instantForce;
            float instantForceMagnitude;
            Vector3 direction;

            instantForceMagnitude = body.driveForce[0];
            direction = body.transform.rotation * body.anchorRotation * -Vector3.right; // get the direction of the force in world space
            // -x because to get the direction of the force
            direction = transform.InverseTransformDirection(direction); // convert to local space
            instantForce = direction * instantForceMagnitude;

            forceReadings.Enqueue(instantForce);
            while (forceReadings.Count > SmoothOverFramesNum) forceReadings.Dequeue(); // remove oldest readings

            // Calculate the moving average
            Vector3 sum =  Vector3.zero;
            foreach (Vector3 f in forceReadings)
            {
                sum += f;
            }
            Force = sum / forceReadings.Count;
            if (recomputeEquationMtx)
            {
                UpdateMatrix();
                recomputeEquationMtx = false;
            }
            LoadCellReadings = GetLoadCellReadings(Force);
            return true;
        }


        /// <summary>
        /// Calculates the reading of individual load cells from the force vector.
        /// </summary>
        /// <param name="forceVector">The force vector to calculate load cell readings from.</param>
        /// <returns>The load cell readings (kg).</returns>
        private Vector3 GetLoadCellReadings(Vector3 forceVector)
        {
            Vector3 b = new Vector3(-forceVector.y, -PlateWidth*forceVector.z, -PlateWidth*forceVector.x);
            return math.mul(math.inverse(equationMatrix), b)/Physics.gravity.magnitude;;
        }
        private void UpdateMatrix()
        {
            equationMatrix = new float3x3(
                1, 1, 1,
                loadCell1.transform.localPosition.z, loadCell2.transform.localPosition.z, loadCell3.transform.localPosition.z,
                loadCell1.transform.localPosition.x, loadCell2.transform.localPosition.x, loadCell3.transform.localPosition.x
            );
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
    }
}