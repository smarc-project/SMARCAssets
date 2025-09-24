using System;
using System.Linq;
using DefaultNamespace.Water;
using ROS.Core;
using Unity.Robotics.Core;
using UnityEngine;


namespace Force
{
    public class ForcePoint : MonoBehaviour
    {
        [Header("Connected Body")] public ArticulationBody ConnectedArticulationBody;
        public Rigidbody ConnectedRigidbody;


        [Header("Buoyancy")]
        [Tooltip("Because HDRP water level queries are expensive at high frequency, you might want to limit it to something managable. 50 is a good starting point. -1 to query every fixed update.")]
        public float WaterQueryFrequency = -1f;

        [Tooltip("GameObject that we will calculate the volume of. Set volume below to 0 to use.")]
        public GameObject VolumeObject;

        [Tooltip("If the gameObject above has many meshes, set the one to use for volume calculations here.")]
        public Mesh VolumeMesh;

        [Tooltip("If not zero, will be used for buoyancy calculations. If zero, the volumeObject/Mesh above will be used to calculate.")]
        public float Volume;

        public float WaterDensity = 997; // kg/m3

        [Tooltip("How deep should the point be to apply the entire buoyancy force. Force is applied proportionally.")]
        public float DepthBeforeSubmerged = 0.03f;

        [Tooltip("Maximum force applied by buoyancy. Nice to keep things from going to space :)")]
        public float MaxBuoyancyForce = 1000f;

        [Header("Underwater/Air Drag")] [Tooltip("Linear Drag applied while underwater. Sets the connected body's drag/linearDamping value when underwater. Set to -1 to use the starting drag value of the body for this.")]
        public float UnderwaterDrag = -1f;

        [Tooltip("Angular Drag applied while underwater. Sets the connected body's drag/linearDamping value when underwater. Set to -1 to use the starting drag value of the body for this.")]
        public float UnderwaterAngularDrag = -1f;

        [Tooltip("Linear Drag applied while underwater. Sets the connected body's drag/linearDamping value when above water. Set to -1 to use the starting drag value of the body for this.")]
        public float AirDrag = -1f;

        [Tooltip("Angular Drag applied while underwater. Sets the connected body's drag/linearDamping value when above water. Set to -1 to use the starting drag value of the body for this.")]
        public float AirAngularDrag = -1f;

        [Header("Current state wrt water level")]
        public bool IsUnderwater = false;

        public bool IsSubmerged = false;
        public float CurrentDepth;


        [Header("Gravity")] [Tooltip("Do we over-ride the gravity of the connected body?")]
        public bool AddGravity = false;

        [Tooltip("If true, calculates center of gravity from all the ForcePoints on the body and overrides the body's centerOfMass, otherwise the centerOfMass of the connected body is used.")]
        public bool AutomaticCenterOfGravity = false;

        [Tooltip("If not zero, will be used for gravity force. If zero, the connected body's mass will be used instead.")]
        public float Mass;


        [Header("Debug")] public bool DrawForces = false;
        public Vector3 AppliedBuoyancyForce, AppliedGravityForce;
        public bool ApplyCustomForce = false;
        public Vector3 CustomForce = Vector3.zero;


        private MixedBody body;
        private WaterQueryModel waterModel;
        private FrequencyTimer waterQueryTimer;
        private ForcePoint[] allForcePoints;


        public Vector3 ApplyForce(Vector3 force, bool onlyUnderWater = false, bool onlyAboveWater = false)
        {
            Vector3 appliedForce = Vector3.zero;
            bool enabled = true;
            if (onlyAboveWater) enabled = !IsSubmerged;
            if (onlyUnderWater) enabled = IsUnderwater;
            if (onlyAboveWater && onlyUnderWater) enabled = false;

            if (enabled)
            {
                appliedForce = force / allForcePoints.Length;
                body.AddForceAtPosition
                (
                    appliedForce,
                    transform.position,
                    ForceMode.Force
                );
            }

            return appliedForce;
        }


        public void Awake()
        {
            body = new MixedBody(ConnectedArticulationBody, ConnectedRigidbody);

            if (!body.isValid)
            {
                Debug.LogWarning($"{gameObject.name} requires at least one of ConnectedArticulationBody or ConnectedRigidBody to be set!");
                return;
            }

            if (AirDrag == -1) AirDrag = body.drag;
            if (AirAngularDrag == -1) AirAngularDrag = body.angularDrag;
            if (UnderwaterDrag == -1) UnderwaterDrag = body.drag;
            if (UnderwaterAngularDrag == -1) UnderwaterAngularDrag = body.angularDrag;

            // If the force point is doing the gravity, disable the body's own
            if (AddGravity)
            {
                body.useGravity = false;
                if (Mass == 0) Mass = body.mass;
            }

            var waterModels = FindObjectsByType<WaterQueryModel>(FindObjectsSortMode.None);
            if (waterModels.Length > 0) waterModel = waterModels[0];

            allForcePoints = body.gameObject.GetComponentsInChildren<ForcePoint>();
            if (AutomaticCenterOfGravity)
            {
                body.automaticCenterOfMass = false;
                var centerOfMass = allForcePoints.Select(point => point.transform.localPosition).Aggregate(new Vector3(0, 0, 0), (s, v) => s + v);
                body.centerOfMass = centerOfMass / allForcePoints.Length;
            }

            if (VolumeMesh == null && VolumeObject != null) VolumeMesh = VolumeObject.GetComponent<MeshFilter>().mesh;
            if (Volume == 0 && VolumeMesh != null) Volume = MeshVolume.CalculateVolumeOfMesh(VolumeMesh, VolumeObject.transform.lossyScale);

            waterQueryTimer = new FrequencyTimer(WaterQueryFrequency);
        }

        void UpdateDepth()
        {
            if (waterModel == null)
            {
                var waterModels = FindObjectsByType<WaterQueryModel>(FindObjectsSortMode.None);
                if (waterModels.Length <= 0) return;
                waterModel = waterModels[0];
            }

            float waterSurfaceLevel = waterModel.GetWaterLevelAt(transform.position);
            CurrentDepth = waterSurfaceLevel - transform.position.y;
            IsSubmerged = CurrentDepth >= DepthBeforeSubmerged;
            IsUnderwater = CurrentDepth > 0;
        }

        // Volume * Density * Gravity
        void FixedUpdate()
        {
            if (Physics.simulationMode == SimulationMode.FixedUpdate) DoUpdate();
        }

        public void DoUpdate()
        {
            var forcePointPosition = transform.position;
            if (AddGravity)
            {
                AppliedGravityForce = ApplyForce(Mass * Physics.gravity);

                if (DrawForces) Debug.DrawLine(forcePointPosition, forcePointPosition + AppliedGravityForce, Color.red, 0.1f);
            }

            // Only update water level related stuff at a limited frequency to avoid performance hits
            if (waterQueryTimer.ExhaustTicks(Clock.Now)) // returns true if any ticks were exhausted or if frequency is <= 0
            {
                UpdateDepth();
                // the forces applied need to be scaled up by the ratio of fixedupdate rate to water query rate
                // since AddForceAtPosition assumes the force is per fixedupdate
                // otherwise, if the water query rate is lower than fixedupdate rate, the forces will
                // be under-applied
                float waterForceScale = WaterQueryFrequency > 0 ? 1f / Time.fixedDeltaTime / WaterQueryFrequency : 1f;
                if (IsUnderwater)
                {
                    float displacementMultiplier = Mathf.Clamp01(CurrentDepth / DepthBeforeSubmerged);
                    var buoyancyForceMag = Volume * WaterDensity * Math.Abs(Physics.gravity.y) * displacementMultiplier;
                    buoyancyForceMag = Mathf.Min(MaxBuoyancyForce, buoyancyForceMag);
                    var buoyancyForce = new Vector3(0, buoyancyForceMag, 0);

                    AppliedBuoyancyForce = ApplyForce(waterForceScale * buoyancyForce, onlyUnderWater: true);

                    if (DrawForces) Debug.DrawLine(forcePointPosition, forcePointPosition + AppliedBuoyancyForce, Color.blue, 0.1f);
                }

                // change the drag of the body to underwater if any is point is. This is a ad-hoc way to 
                // simulate the sticktion water usually applies to objects
                // also, some objects might need to be useful under AND over water (like ropes...)
                // and their drag really should reflect where they are moment to moment
                // yes, all of the points will do the same thing. but this makes it so we dont need
                // a central forcepoint controller or sth
                var anyUnderwater = allForcePoints.Select(p => p.IsUnderwater).Aggregate(false, (s, v) => s || v);
                body.drag = anyUnderwater ? UnderwaterDrag : AirDrag;
                body.angularDrag = anyUnderwater ? UnderwaterAngularDrag : AirAngularDrag;
            }



            // And lastly, whatever custom force was set.
            if (ApplyCustomForce) ApplyForce(CustomForce);
        }
    }
}