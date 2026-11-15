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
        public Transform loadCell1;
        public Transform loadCell2;
        public Transform loadCell3;
        [Tooltip("Optional. Leave empty to use 3 load cells.")]
        public Transform loadCell4;
        [SerializeField, Min(1)]
        private int SmoothOverFramesNum = 1; // size of the queue
        [SerializeField]
        private float LoadCellRadius = 0.2f; // radius of the load cells placement
        [SerializeField]
        private float RopeLeverArm = 0.05f; // lever arm of the rope attachment point
        [SerializeField]
        private bool DebugMode = false; // if true, draw the force vector
        [SerializeField]
        private float DebugForceScale = 0.5f; // scales the length of the drawn force vector
        [SerializeField]
        private float DebugLineThickness = 10f; // thickness of the drawn force vector in pixels

        [Header("Outputs")]
        public float[] LoadCellReadings = new float[3]; // readings of the individual load cells
        public Vector3 Force;

        private Transform[] loadCells;
        // we use (pseudo)inverse of matrix encoding the equations to convert the force vector to load cell readings
        // specifically At(A At)^-1, which equals A^-1 for 3 load cells
        private float3x3 AAtInv; // (A A^T)^-1
        private Queue<Vector3> forceReadings = new(); // Queue to store the last N force readings for moving average
        private bool recomputeEquationMtx = true; 
        private float3[] cellsCols;


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
            loadCells = GetLoadCells();
            LoadCellReadings = new float[loadCells.Length];
            recomputeEquationMtx = true;
        } 

        private Transform[] GetLoadCells()
        {
            return loadCell4 == null ? new Transform[] {loadCell1, loadCell2, loadCell3} 
                                    : new Transform[] {loadCell1, loadCell2, loadCell3, loadCell4};
        }

        void OnValidate()
        {
            // reposition the load cells
            if (loadCell1 != null && loadCell2 != null && loadCell3 != null)
            {
                Transform [] cells = GetLoadCells();
                float offset = cells.Length == 4 ? Mathf.PI/4 : 0; // offset the angle for 4 LC to start in the corner
                float angle;
                for (int i = 0; i < cells.Length; i++)
                {
                    angle = i*2*Mathf.PI/cells.Length + offset;
                    cells[i].transform.localPosition = new Vector3(LoadCellRadius * Mathf.Sin(angle), 0, LoadCellRadius * Mathf.Cos(angle));
                }
                loadCells = cells;
                LoadCellReadings = new float[cells.Length];
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
        private float[] GetLoadCellReadings(Vector3 forceVector)
        {
            float[] res = new float[loadCells.Length];
            Vector3 b = new Vector3(-forceVector.y, -RopeLeverArm*forceVector.z, -RopeLeverArm*forceVector.x);
            Vector3 y = math.mul(AAtInv, b);
            for (int i = 0; i <loadCells.Length; i++)
            {
                res[i] = math.dot(cellsCols[i],y)/Physics.gravity.magnitude;
            }

            return res;
        }

        /// <summary>
        /// Creates matrix describing the force and torque balance equations that transform the force to load cell readings.
        /// Equations:
        /// force balance: sum(F_i) = -F_y
        /// torque balance 1: sum(z_i * F_i) = - ROPE_LEVER_ARM * F_z
        /// torque balance 2: sum(x_i * F_i) = - ROPE_LEVER_ARM * F_x
        /// </summary>
        private void UpdateMatrix()
        {
            cellsCols = new float3[loadCells.Length]; // columns of the equation matrix
            for (int i = 0; i < loadCells.Length; i++)
            {
                cellsCols[i] = new float3(1, loadCells[i].transform.localPosition.z, loadCells[i].transform.localPosition.x);
            }
            float3x3 AAt = new float3x3();
            // AAt = [c1 c2 c3] * [c1t c2t c3t] = sum(ci * cit)
            for (int i = 0; i < loadCells.Length; i++)
            {
                AAt += new float3x3(cellsCols[i]*cellsCols[i][0], cellsCols[i]*cellsCols[i][1], cellsCols[i]*cellsCols[i][2]);
            }
            if (math.abs(math.determinant(AAt)) < 1e-9f)
            {
                Debug.LogWarning("[LoadCells3D] The load cells are collinear, readings are invalid.");
            }
            AAtInv = math.inverse(AAt);
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