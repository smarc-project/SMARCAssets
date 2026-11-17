using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using NormalDistribution = DefaultNamespace.NormalDistribution;

namespace VehicleComponents.Sensors
{
    public enum SonarType
    {
        FLS,
        SSS,
        MBES,
        FLS3D15
    }

    [AddComponentMenu("Smarc/Sensor/Sonar")]
    public class Sonar : Sensor
    {
        [Header("Sonar")]
        public SonarType Type = SonarType.MBES;
        [Tooltip("Number of rays per beam (a fan of rays).")]
        public int NumRaysPerBeam = 500;
        [Tooltip("Total opening angle of _each_ beam.")]
        public float BeamBreadthDeg = 90;
        [Tooltip("How many beams(fans) are in this arrangement of sonar. For FLS, they will be arranged left-to-right with fan opening in the forward axis. For SSS and MBES, they will be arranged left-to-right with fan opening also left-to-right.")]
        public int NumBeams = 1;
        public float MaxRange = 100;
        [Tooltip("-3dB opening angle of each beam. For beam-pattern related return intensity calculations.")]
        public float BeamBreadth3DecibelsDeg = 60;
        [Tooltip("Angle from the forward-right plane (usually horizontal-ish) of the beam. For SSS, 180-(2*(tilt+BeamBreath)) = nadir. For FLS, just the tilt downwards.")]        
        public float TiltAngleDeg = 15;
        [Tooltip("For FLS: FOV of the beams")]
        public float FLSFOVDeg = 30;

        [Header("SideScanSonar")]
        [Tooltip("There might be fewer pixels(buckets) than rays being cast.")]
        public int NumBucketsPerBeam = 1000;
        [Tooltip("Is this a normal SSS or an interferometric one?")]
        public bool isInterferometric = false;

        [Header("SSS-Noise")]
        public float MultGain = 4;
        public bool UseAdditiveNoise = true;
        public float AddNoiseStd = 1;
        public float AddNoiseMean = 0;

        NormalDistribution additiveNormal;

        [HideInInspector] public byte[] Buckets;
        [HideInInspector] public byte[] BucketsAngleHigh;
        [HideInInspector] public byte[] BucketsAngleLow;
        int totalBuckets => NumBucketsPerBeam * NumBeams;

        [HideInInspector] public SonarHit[] SonarHits;
        [HideInInspector] public float HitsMinHeight = Mathf.Infinity;
        [HideInInspector] public float HitsMaxHeight = 0f;

        [Header("Load")]
        public float TimeShareInFixedUpdate;

        JobHandle handle;
        NativeArray<RaycastHit> results;
        protected NativeArray<RaycastCommand> commands;
        NativeArray<Vector3> localRayDirections;
        bool hasPendingRaycasts;
        float nextSlowUpdateWarningTime;
        (SonarType type, int rays, int beams, float breadth, float tilt, float fov) cachedRayGeometry;

        public virtual int TotalRayCount => NumRaysPerBeam * NumBeams;
        public virtual int HorizontalResolution => NumRaysPerBeam;
        public virtual int VerticalResolution => NumBeams;
        public float DegreesPerRayInBeam => NumRaysPerBeam > 1 ? BeamBreadthDeg / (NumRaysPerBeam - 1) : 0f;
        public float DegreesPerBeamInFLS => NumBeams > 1 ? FLSFOVDeg / (NumBeams - 1) : 0f;
        [HideInInspector] public List<float> BeamProfile;

        new protected virtual void OnValidate()
        {
            base.OnValidate();
            ConfigureGeometry();
        }

        protected virtual void ConfigureGeometry()
        {
            if (Type == SonarType.FLS3D15)
            {
                Debug.LogError("Use the Sonar3D component for FLS3D15 sonar.", this);
                enabled = false;
                return;
            }
            if (Type == SonarType.SSS) NumBeams = 2;
            if (Type == SonarType.MBES)
            {
                NumBeams = 1;
                TiltAngleDeg = -1f;
            }
            NumRaysPerBeam = Mathf.Max(1, NumRaysPerBeam);
            NumBeams = Mathf.Max(1, NumBeams);
        }

        new protected virtual void Awake()
        {
            base.Awake();
            ConfigureGeometry();
            InitHits();
            InitBeamProfileSimple();
            if (Type == SonarType.SSS) InitSidescanBuckets();
        }

        protected virtual void OnDestroy()
        {
            handle.Complete();
            DisposeRaycastBuffers();
        }

        protected virtual void PrepareScan() { }

        // Return whether the completed scan belongs to a different beam layout.
        protected virtual bool UpdateRayGeometryKey()
        {
            var geometry = (Type, NumRaysPerBeam, NumBeams, BeamBreadthDeg, TiltAngleDeg, FLSFOVDeg);
            bool changed = geometry != cachedRayGeometry;
            cachedRayGeometry = geometry;
            return changed;
        }

        void InitSidescanBuckets()
        {
            Buckets = new byte[totalBuckets];
            BucketsAngleHigh = new byte[totalBuckets];
            BucketsAngleLow = new byte[totalBuckets];
            additiveNormal = new NormalDistribution(AddNoiseMean, AddNoiseStd);
        }

        protected void InitHits()
        {
            SonarHits = new SonarHit[TotalRayCount];
            for(int i=0; i<TotalRayCount; i++)
            {
                SonarHits[i] = new SonarHit(this);
            }
        }

        void InitBeamProfileGaussian()
        {
            float CalculateGaussianIntensity(float beamAngle, float beamCenter, float sigma)
            {
                var gaussianIntensity = Mathf.Exp(-(Mathf.Pow(beamAngle - beamCenter, 2) / (2 * Mathf.Pow(sigma, 2))));
                return gaussianIntensity;
            }
            var angleStepDeg = BeamBreadthDeg / (NumRaysPerBeam - 1.0f);
            var fwhmSigma = BeamBreadth3DecibelsDeg / (2 * Mathf.Sqrt(2.0f * Mathf.Log(2.0f)));
            BeamProfile = new List<float>();
            for(int i=0; i<NumRaysPerBeam; i++)
            {
                var beamAngleDeg = -BeamBreadthDeg / 2 + i * angleStepDeg;
                float intensity =
                    CalculateGaussianIntensity(beamAngle: beamAngleDeg, beamCenter: 0.0f, sigma: fwhmSigma);
                BeamProfile.Add(intensity);
            }
        }

        protected void InitBeamProfileSimple()
        {
            BeamProfile = new List<float>();
            for(int i=0; i<NumRaysPerBeam; i++)
            {
                BeamProfile.Add(1.0f);
            }
        }

        public static (int, int) BeamNumRayNumFromRayIndex(int i, int NumRaysPerBeam)
        {
            var rayNum = i % NumRaysPerBeam;
            int beamNum = (int)i / (int)NumRaysPerBeam;
            return (beamNum, rayNum);
        }

        protected virtual void UpdateSonarHits(NativeArray<RaycastHit> rayResults)
        {
            HitsMinHeight = Mathf.Infinity;
            HitsMaxHeight = 0f;
            UpdateSonarHitsPhysics(rayResults);
            if (Type == SonarType.SSS) UpdateSidescan();
        }

        protected void UpdateSonarHitsPhysics(NativeArray<RaycastHit> rayResults)
        {
            for(int i=0; i < TotalRayCount; i++)
            {
                var hit = rayResults[i];
                var (beamNum, rayNum) = BeamNumRayNumFromRayIndex(i, NumRaysPerBeam);
                SonarHits[i].Update(hit, BeamProfile[rayNum]);
                if(hit.collider != null && hit.point.y > HitsMaxHeight && hit.point.y<0) HitsMaxHeight = hit.point.y;
                if(hit.collider != null && hit.point.y < HitsMinHeight) HitsMinHeight = hit.point.y;
            }
        }

        // Complete and consume the previous batch before reusing its storage.
        bool EnsureRaycastBuffers()
        {
            int count = TotalRayCount;
            if (results.IsCreated && results.Length == count) return false;

            DisposeRaycastBuffers();
            results = new NativeArray<RaycastHit>(count, Allocator.Persistent);
            commands = new NativeArray<RaycastCommand>(count, Allocator.Persistent);
            localRayDirections = new NativeArray<Vector3>(count, Allocator.Persistent);
            return true;
        }

        void DisposeRaycastBuffers()
        {
            if (results.IsCreated) results.Dispose();
            if (commands.IsCreated) commands.Dispose();
            if (localRayDirections.IsCreated) localRayDirections.Dispose();
        }

        protected virtual Vector3 CalculateLocalRayDirection(int i)
        {
            var (beam, ray) = BeamNumRayNumFromRayIndex(i, NumRaysPerBeam);
            float rayAngle = ray * DegreesPerRayInBeam;
            switch (Type)
            {
                case SonarType.MBES:
                    return Quaternion.AngleAxis(rayAngle - BeamBreadthDeg / 2f, Vector3.forward) * -Vector3.up;
                case SonarType.FLS:
                    return Quaternion.AngleAxis(beam * DegreesPerBeamInFLS - FLSFOVDeg / 2f, Vector3.up)
                        * (Quaternion.AngleAxis(rayAngle + TiltAngleDeg, Vector3.right) * Vector3.forward);
                case SonarType.SSS:
                    float side = beam * 2 - 1;
                    rayAngle -= BeamBreadthDeg / 2f;
                    rayAngle += side * (90f - TiltAngleDeg - BeamBreadthDeg / 2f);
                    return Quaternion.AngleAxis(rayAngle, Vector3.forward) * -Vector3.up;
                default:
                    return -Vector3.up;
            }
        }

        public override bool UpdateSensor(double deltaTime)
        {
            PrepareScan();

            var t0 = Time.realtimeSinceStartup;
            bool hasNewScan = false;
            bool geometryChanged = UpdateRayGeometryKey();
            if (hasPendingRaycasts)
            {
                handle.Complete();
                hasPendingRaycasts = false;
                // An old scan cannot be interpreted using a different beam layout.
                if (!geometryChanged)
                {
                    UpdateSonarHits(results);
                    hasNewScan = true;
                }
            }

            bool resized = EnsureRaycastBuffers();
            if (resized || geometryChanged)
            {
                for (int i = 0; i < localRayDirections.Length; i++)
                    localRayDirections[i] = CalculateLocalRayDirection(i);

                if (SonarHits == null || SonarHits.Length != TotalRayCount) InitHits();
                if (BeamProfile == null || BeamProfile.Count != NumRaysPerBeam) InitBeamProfileSimple();
                if (Type == SonarType.SSS
                    && (Buckets == null || Buckets.Length != totalBuckets)) InitSidescanBuckets();
            }

            var setupJob = new SetupSonarRaycastJob
            {
                Commands = commands,
                LocalDirections = localRayDirections,
                SonarRotation = transform.rotation,
                SonarPosition = transform.position,
                MaxRange = MaxRange
            };

            handle = setupJob.Schedule(commands.Length, 10, default(JobHandle));
            handle = RaycastCommand.ScheduleBatch(commands, results, 20, handle);
            hasPendingRaycasts = true;

            var t1 = Time.realtimeSinceStartup;
            TimeShareInFixedUpdate = (t1-t0)/Time.fixedDeltaTime;
            if(TimeShareInFixedUpdate > 0.5f && Time.unscaledTime >= nextSlowUpdateWarningTime)
            {
                nextSlowUpdateWarningTime = Time.unscaledTime + 1f;
                Debug.LogWarning($"Sonar '{name}' took more than half the time in a fixedUpdate!", this);
            }

            return hasNewScan;
        }

        void UpdateSidescan()
        {
            if(Type != SonarType.SSS) return;
            
            Array.Clear(Buckets, 0, Buckets.Length);
            Array.Clear(BucketsAngleHigh, 0, BucketsAngleHigh.Length);
            Array.Clear(BucketsAngleLow, 0, BucketsAngleLow.Length);

            int[] cnt = new int[Buckets.Length];
            float[] bucketsSum = new float[Buckets.Length];
            float[] bucketsAngleHighSum = new float[Buckets.Length];
            float[] bucketsAngleLowSum = new float[Buckets.Length];

            float minDistance = 0;
            float bucketSize = (MaxRange - minDistance) / NumBucketsPerBeam;
            var angleStepDeg = BeamBreadthDeg / (NumRaysPerBeam - 1.0f);

            for(int rayIndex = 0; rayIndex < TotalRayCount; rayIndex++)
            {
                var (beamNum, rayNum) = Sonar.BeamNumRayNumFromRayIndex(rayIndex, NumRaysPerBeam);
                var sh = SonarHits[rayIndex];

                double addNoise = 0;
                if(UseAdditiveNoise) addNoise = additiveNormal.Sample();

                float dis = (float)(sh.Hit.distance + addNoise);
                if(dis<0) dis=0;
                int bucketIndexInBeam = Mathf.FloorToInt((dis - minDistance)/bucketSize);
                if(bucketIndexInBeam >= NumBucketsPerBeam || bucketIndexInBeam < 0) continue;
                int bucketIndex = bucketIndexInBeam + beamNum*NumBucketsPerBeam;
                bucketsSum[bucketIndex] += (sh.ReturnIntensity * 255 * MultGain);
                
                if(isInterferometric)
                {
                    float beamAngleDeg;
                    if (beamNum==0) beamAngleDeg = -TiltAngleDeg - rayIndex * angleStepDeg;
                    else beamAngleDeg = -TiltAngleDeg - (TotalRayCount - rayIndex) * angleStepDeg;
                    
                    ushort magicNumber = 20860;
                    ushort angle_uint16 = (ushort) ( (Mathf.PI+beamAngleDeg*Mathf.Deg2Rad) * magicNumber);
                    byte angle_low = (byte) (angle_uint16 & 0xff);
                    byte angle_high = (byte) ((angle_uint16 >> 8) & 0xff);
                    bucketsAngleHighSum[bucketIndex] += angle_high;
                    bucketsAngleLowSum[bucketIndex] += angle_low;
                }

                cnt[bucketIndex]++;
            }

            for(int bucketIndex = 0; bucketIndex < Buckets.Length; bucketIndex++)
            {
                if(cnt[bucketIndex] == 0) continue;
                Buckets[bucketIndex] = (byte) (bucketsSum[bucketIndex]/cnt[bucketIndex]);

                if(isInterferometric)
                {
                    BucketsAngleHigh[bucketIndex] = (byte) (bucketsAngleHighSum[bucketIndex]/cnt[bucketIndex]);
                    BucketsAngleLow[bucketIndex] = (byte) (bucketsAngleLowSum[bucketIndex]/cnt[bucketIndex]);
                }
            }
        }

        [BurstCompile]
        struct SetupSonarRaycastJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Vector3> LocalDirections;
            public NativeArray<RaycastCommand> Commands;
            public Quaternion SonarRotation;
            public Vector3 SonarPosition;
            public float MaxRange;

            public void Execute(int i)
            {
                Vector3 direction = SonarRotation * LocalDirections[i];
                Commands[i] = new RaycastCommand(SonarPosition, direction, QueryParameters.Default, MaxRange);
            }
        }
    }
}
