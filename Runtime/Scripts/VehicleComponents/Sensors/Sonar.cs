using System; //Bit converter
using UnityEngine;
using System.Collections.Generic;

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using NormalDistribution = DefaultNamespace.NormalDistribution;


namespace VehicleComponents.Sensors
{
    public class SonarHit
    {
        public RaycastHit Hit;
        public float ReturnIntensity;
        public int MaterialLabel;
        Sonar sonar;

        public static readonly Dictionary<string, float> simpleMaterialReflectivity = new Dictionary<string, float>()
        {
            {"Rock", 0.8f},
            {"Mud", 0.2f},
            {"Buoy", 0.99f},  // Buoy, Algae, and Rope are currently just wild guesses
            {"Algae", 0.25f},
            {"Rope", 0.4f}
        };

        public static readonly Dictionary<string, int> materialLabels = new Dictionary<string, int>()
        {
            // Labels are assigned based on importance, lower values will be over-written by higher values 
            {"Rock", 1},
            {"Mud", 1},
            {"Buoy", 3},  // Buoy, Algae, and Rope are currently just wild guesses
            {"Algae", 2},
            {"Rope", 4}
        };

        public SonarHit(Sonar sonar)
        {
            ReturnIntensity = -1;
            MaterialLabel = 0;
            this.sonar = sonar;
        }

        public void Update(RaycastHit hit, float beam_intensity)
        {
            
            ReturnIntensity = GetIntensity(beam_intensity);
            MaterialLabel = GetMaterialLabel();
            this.Hit = hit;
        }

        static string CleanUpMaterialName(string name)
        {
            // name can have " (instance of)" added to it,
            // remove that...
            if(name.Contains("(")) return name.Split("(")[0].Trim();
            return name;
        }


        public float GetMaterialReflectivity()
        {
            // Return some default value for things that dont hit
            // 0 intensity = no hit
            if(!(Hit.collider)) return 0f;
            if(!(Hit.collider.material)) return 0.5f;

            string name = CleanUpMaterialName(Hit.collider.material.name);

            // if its a simple one, just return that
            if(simpleMaterialReflectivity.ContainsKey(name)) return simpleMaterialReflectivity[name];
            // if its a complex material that we want a function for,
            // switch for it here?
            // TODO that switch lol
            return 0.5f;
        }
        
        public int GetMaterialLabel()
        {
            // Return some default value for things that dont hit
            // 0 is default for no hit, hit w/o material, hit w/o named material no is materialLabels{}
            if(!(Hit.collider)) return 0;
            if(!(Hit.collider.material)) return 0;

            string name = CleanUpMaterialName(Hit.collider.material.name);

            // Return the label
            if(materialLabels.ContainsKey(name)) return materialLabels[name];
            // if the named material has ne specified label
            return 0;
        }

        public float GetIntensity(float beamIntensity)
        {
            // intensity of hit between 1-255
            // It is a function of
            // 1) The distance traveled by the beam -> distance
            float hitDistIntensity = (sonar.MaxRange - Hit.distance) / sonar.MaxRange;

            // 2) The angle of hit -> angle between the ray and normal
            // the hit originated from transform position, and hit sonarHit
            float hitAngle = Vector3.Angle(sonar.transform.position - Hit.point, Hit.normal);
            float hitAngleIntensity = Mathf.Abs(Mathf.Cos(hitAngle*Mathf.Deg2Rad));

            // 3) The properties of the point of hit -> material
            // if available, use the material of the hit object to determine the reflectivitity.
            float hitMaterialIntensity = GetMaterialReflectivity();

            // Lambert's cosine law:
            // Intensity = K * Ensonification at point * Reflectivity at p * abs(cos(incidence angle at p))
            // We just set K = 1 here.
            // Ensonification = distance traveled
            // Reflectivity = material prop.
            // Angle is obvious.
            // beamIntensity accounts for the angular dependence of the ensonification intensity, beam profile
            float intensity = beamIntensity * hitDistIntensity * hitAngleIntensity * hitMaterialIntensity;
            if(intensity > 1) intensity=1;
            if(intensity < 0) intensity=0;

            
            return intensity;
        }

        public byte[] GetBytes()
        {
            // so first, we gotta convert the unity points to ros points
            // then x,y,z need to be byte-ified
            // then a fourth "intensity" needs to be created and byte-ified
            var point = Hit.point.To<ENU>();

            var xb = BitConverter.GetBytes(point.x);
            var yb = BitConverter.GetBytes(point.y);
            var zb = BitConverter.GetBytes(point.z);

            byte[] ib = {(byte)(ReturnIntensity*255)};

            int totalBytes = xb.Length + yb.Length + zb.Length+ ib.Length;
            byte[] ret = new byte[totalBytes];
            // src, offset, dest, offset, count
            // Imma hard-code the offsets and counts, to act as a weird
            // error catching mechanism
            Buffer.BlockCopy(xb, 0, ret, 0, 4);
            Buffer.BlockCopy(yb, 0, ret, 4, 4);
            Buffer.BlockCopy(zb, 0, ret, 8, 4);
            Buffer.BlockCopy(ib, 0, ret, 12,1);

            return ret;
        }
    }


    public enum SonarType
    {
        FLS,
        SSS,
        MBES
    }


    public class Sonar : Sensor
    {

        [Header("Sonar")]
        public SonarType Type = SonarType.MBES;
        [Tooltip("Numer of rays cast per beam. Beam = A fan of rays.")]
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

        //  The SideScan pixels
        [HideInInspector] public byte[] Buckets;
        [HideInInspector] public byte[] BucketsAngleHigh;
        [HideInInspector] public byte[] BucketsAngleLow;
        int totalBuckets => NumBucketsPerBeam * NumBeams;


        // we use this one to keep the latest hits in memory and
        // accessible to outside easily.
        [HideInInspector] public SonarHit[] SonarHits;
        // Keeping track of lowest and highest hit heights
        // for visualization or other purposes
        [HideInInspector] public float HitsMinHeight = Mathf.Infinity;
        [HideInInspector] public float HitsMaxHeight = 0f;
        


        [Header("Load")]
        public float TimeShareInFixedUpdate;


        // Unity job structure for long-term casting of rays.
        private JobHandle handle;
        private NativeArray<RaycastHit> results;
        private NativeArray<RaycastCommand> commands;

        [HideInInspector] public int TotalRayCount => NumRaysPerBeam * NumBeams;
        [HideInInspector] public float DegreesPerRayInBeam => BeamBreadthDeg/(NumRaysPerBeam-1);
        [HideInInspector] public float DegreesPerBeamInFLS => FLSFOVDeg/(NumBeams-1);

        [HideInInspector] public List<float> BeamProfile;


        new protected void OnValidate()
        {
            base.OnValidate();
            if (Type == SonarType.SSS) NumBeams = 2;
            if (Type == SonarType.MBES)
            {
                NumBeams = 1;  
                TiltAngleDeg = -1;
            }
            if (NumRaysPerBeam <= 0) NumRaysPerBeam = 1;
        }

        new protected void Awake()
        {
            base.Awake();
            InitHits();
            InitBeamProfileSimple();
            if (Type == SonarType.SSS) InitSidescanBuckets();
        }

        void InitSidescanBuckets()
        {
            // Each bucket has a 1 byte intensity value 0-255
            Buckets = new byte[totalBuckets];

            // followed by 2 bytes angle value 0-65535 [-pi,0]
            // angle is in radians, but we store it as a 16bit unsigned int
            // so we can have a resolution of pi/65535, the magic number is 20860
            BucketsAngleHigh = new byte[totalBuckets];
            BucketsAngleLow = new byte[totalBuckets];

            additiveNormal = new NormalDistribution(AddNoiseMean, AddNoiseStd);
        }

        void InitHits()
        {
            // Initialize all the hits as empty so we can just update them later
            // rather than spamming new ones
            SonarHits = new SonarHit[TotalRayCount];
            for(int i=0; i<TotalRayCount; i++)
            {
                SonarHits[i] = new SonarHit(this);
            }
        }

        void InitBeamProfileGaussian()
        {
            // Initialize the Gaussian beam profile
            // The Gaussian is specified be its full width half max (fwhm), 
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
        
        void InitBeamProfileSimple()
        {
            // Initialize simple beam profile
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

        void UpdateSonarHits(NativeArray<RaycastHit> results)
        {
            // TODO this should be done in parallel too...?
            for(int i=0; i < TotalRayCount; i++)
            {
                var hit = results[i];
                var (beamNum, rayNum) = BeamNumRayNumFromRayIndex(i, NumRaysPerBeam);
                SonarHits[i].Update(hit, BeamProfile[rayNum]);
                if(hit.point.y > HitsMaxHeight && hit.point.y<0) HitsMaxHeight = hit.point.y;
                if(hit.point.y < HitsMinHeight) HitsMinHeight = hit.point.y;
            }
        }
            
            
        public override bool UpdateSensor(double deltaTime)
        {
            var t0 = Time.realtimeSinceStartup;
            if (results.Length > 0)
            {
                // Wait for the batch processing job to complete
                handle.Complete();

                // Update the sonarHit objects with the results of raycasts
                UpdateSonarHits(results);
                // if this is a sidescan, do some extra stuff
                if(Type == SonarType.SSS) UpdateSidescan();

                // Dispose the buffers
                results.Dispose();
                commands.Dispose();
            }


            results = new NativeArray<RaycastHit>(TotalRayCount, Allocator.Persistent);
            commands = new NativeArray<RaycastCommand>(TotalRayCount, Allocator.Persistent);

            var setupJob = new SetupSonarRaycastJob()
            {
                Commands = commands,
                NumRaysPerBeam = NumRaysPerBeam,
                SonarUp = transform.up,
                SonarForward = transform.forward,
                SonarRight = transform.right,
                SonarPosition = transform.position,
                Type = Type,
                DegreesPerRayInBeam = DegreesPerRayInBeam,
                BeamBreadthDeg = BeamBreadthDeg,
                MaxRange = MaxRange,
                TiltAngleDeg = TiltAngleDeg,
                FLSFOVDeg = FLSFOVDeg,
                DegreesPerBeamInFLS = DegreesPerBeamInFLS
            };

            JobHandle deps = setupJob.Schedule(commands.Length, 10, default(JobHandle));
            handle = RaycastCommand.ScheduleBatch(commands, results, 20, deps);

            var t1 = Time.realtimeSinceStartup;
            TimeShareInFixedUpdate = (t1-t0)/Time.fixedDeltaTime;
            if(TimeShareInFixedUpdate > 0.5f) Debug.LogWarning($"Sonar in {transform.parent.name}/{transform.name} took more than half the time in a fixedUpdate!");

            return true;
        }

        void UpdateSidescan()
        {
            if(Type != SonarType.SSS) return;
            
            // 0-out, since maybe not the same buckets will be written to.
            Array.Clear(Buckets, 0, Buckets.Length);
            Array.Clear(BucketsAngleHigh, 0, BucketsAngleHigh.Length);
            Array.Clear(BucketsAngleLow, 0, BucketsAngleLow.Length);

            int[] cnt = new int[Buckets.Length];
            float[] bucketsSum = new float[Buckets.Length];
            float[] bucketsAngleHighSum = new float[Buckets.Length];
            float[] bucketsAngleLowSum = new float[Buckets.Length];

            // First we gotta know what distance ranges each bucket needs to
            // have, we can ask the sonar object for its max distance;
            float minDistance = 0;
            float bucketSize = (MaxRange - minDistance) / NumBucketsPerBeam;
            var angleStepDeg = BeamBreadthDeg / (NumRaysPerBeam - 1.0f);

            for(int rayIndex = 0; rayIndex < TotalRayCount; rayIndex++)
            {
                // since buckets is a flat array...
                var (beamNum, rayNum) = Sonar.BeamNumRayNumFromRayIndex(rayIndex, NumRaysPerBeam);
                
                var sh = SonarHits[rayIndex];

                double addNoise = 0;
                if(UseAdditiveNoise) addNoise = additiveNormal.Sample();

                // discritize the ray distance into a bucket
                float dis = (float)(sh.Hit.distance + addNoise);
                if(dis<0) dis=0;
                int bucketIndexInBeam = Mathf.FloorToInt((dis - minDistance)/bucketSize);
                if(bucketIndexInBeam >= NumBucketsPerBeam || bucketIndexInBeam < 0) continue;
                // bucketIndex is where in the specific bucket (usually port/strb 0/1)
                // this ray falls, but we have a flat array of buckets, so gotta place those
                // starboards further down the array
                int bucketIndex = bucketIndexInBeam + beamNum*NumBucketsPerBeam;
                // intensities are stored as floats in [0,1], but we want bytes in 0-255 range
                bucketsSum[bucketIndex] += (sh.ReturnIntensity * 255 * MultGain);
                
                // These are done only if this is an interferometric sidescan
                if(isInterferometric)
                {
                    float beamAngleDeg;
                    if (beamNum==0) beamAngleDeg = -TiltAngleDeg - rayIndex * angleStepDeg;
                    else beamAngleDeg = -TiltAngleDeg - (TotalRayCount - rayIndex) * angleStepDeg;
                    
                    ushort magicNumber = 20860;
                    ushort angle_uint16 = (ushort) ( (Mathf.PI+beamAngleDeg*Mathf.Deg2Rad) * magicNumber);
                    // followed by 2 bytes angle value 0-65535 [-pi,0]
                    // angle is in radians, but we store it as a 16bit unsigned int
                    // so we can have a resolution of pi/65535, the magic number is 20860

                    // We want to have a 16 uint for the angle with high byte and low byte
                    // so we can have a resolution of pi/65535
                    byte angle_low = (byte) (angle_uint16 & 0xff);
                    byte angle_high = (byte) ((angle_uint16 >> 8) & 0xff);
                    bucketsAngleHighSum[bucketIndex] += angle_high;
                    bucketsAngleLowSum[bucketIndex] += angle_low;
                }

                // count how many rays fell into this bucket
                cnt[bucketIndex]++;
            }

            // finally, we can average the rays
            for(int bucketIndex = 0; bucketIndex < Buckets.Length; bucketIndex++)
            {
                if(cnt[bucketIndex] == 0) continue; // no rays in the bucket, left at 0 by default.
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
            public NativeArray<RaycastCommand> Commands;
            public int NumRaysPerBeam;
            public Vector3 SonarUp;
            public Vector3 SonarForward;
            public Vector3 SonarRight;
            public Vector3 SonarPosition;
            public SonarType Type;
            public float DegreesPerRayInBeam;
            public float BeamBreadthDeg;
            public float MaxRange;
            public float TiltAngleDeg;
            public float FLSFOVDeg;
            public float DegreesPerBeamInFLS;

            public void Execute(int i)
            {
                var direction = -SonarUp; // default down?
                var (beamNum, rayNum) = Sonar.BeamNumRayNumFromRayIndex(i, NumRaysPerBeam);
                if(Type == SonarType.MBES)
                {
                    // MBES is just one beam looking down directly. Simplest.
                    // start a beam looking directly down
                    direction = -SonarUp;
                    // we want 0 degrees in the center and then +-Breadth/2 on the sides.
                    var rayAngle = (rayNum * DegreesPerRayInBeam) - BeamBreadthDeg/2;
                    // rotate it around the forward axis by its ray number in the beam
                    // offset half-way so the middle is directly down.
                    direction = Quaternion.AngleAxis(rayAngle, SonarForward) * direction;
                }
                if(Type == SonarType.FLS)
                {
                    // FLS is MBES, but the first ray is not in the center, its at the edge and is tilted.
                    // there are also >1 beams.

                    // we want 0 degrees at the edge and BeamBreadt degrees at the other edge of the beam.
                    var rayAngle = rayNum * DegreesPerRayInBeam;
                    // FLS beams are defined as vertical fans, sweeping side-to-side
                    // so we start a ray forward first.
                    direction = SonarForward;
                    // then we rotate _that_ to the ray angle within the beam, around the side-axis
                    // plus the tilt angle which is measured from the horizontal plane down
                    direction = Quaternion.AngleAxis(rayAngle+TiltAngleDeg, SonarRight) * direction;
                    // then we rotate it to the beam angle, around the UP axis
                    var beamAngle = (beamNum * DegreesPerBeamInFLS) - FLSFOVDeg/2;
                    direction = Quaternion.AngleAxis(beamAngle, SonarUp) * direction;
                }
                if(Type == SonarType.SSS)
                {
                    // SSS usually has 2 beams, port and starboard
                    // their position is measured from the horizontal axis towards the vertical axis
                    // called the tilt angle, so we need to further rotate rays accordingly

                    // start the ray looking down
                    direction = -SonarUp;
                    // spread the beam with 0 degeres in the middle and +/- half-breadth around it
                    var rayAngle = rayNum * DegreesPerRayInBeam - BeamBreadthDeg/2;
                    var side = (beamNum * 2)-1; // port or starboard, -1, +1
                    // tilt it
                    rayAngle += side*(90 - TiltAngleDeg - BeamBreadthDeg/2);
                    direction = Quaternion.AngleAxis(rayAngle, SonarForward) * direction;
                }
                
                // and finally, cast dem rays boi.
                Commands[i] = new RaycastCommand(SonarPosition, direction, QueryParameters.Default, MaxRange);
            }
        }
    }


}
