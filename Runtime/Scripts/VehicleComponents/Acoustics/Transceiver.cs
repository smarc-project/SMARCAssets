using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Robotics.Core; //Clock
using DefaultNamespace.Water; // WaterQueryModel
using Icosphere = DefaultNamespace.IcoSphere;
using ROS.Core;
using SmarcGUI;
using DefaultNamespace;

namespace VehicleComponents.Acoustics
{

    public class StringStamped
    {
        public string Data;
        public double TimeSent;
        public double TimeReceived;
       
        public StringStamped(string data, double timeSent)
        {
            Data = data;
            this.TimeSent = timeSent;
        }

        public void Received(double time)
        {
            TimeReceived = time;
        }
    }


    [RequireComponent(typeof(MeshFilter))]
    public class Transceiver : MonoBehaviour, ISoundVelocityUser, IROSPublishable
    {   
        [Tooltip("Speed of sound underwater, defaults to 1500m/s.")]
        public float SoundVelocity = 1500f;
        
        [Tooltip("Maximum range of this transceiver for broadcasting.")]
        public float MaxRange = 100;
        
        [Tooltip("Min radius of the unoccupied channel. Think of a tube between transceivers free of obstacles. How big should it be to transmit?")]
        public float MinChannelRadius = 0.2f;
        [Tooltip("If checked, transmission will work regardless of occlusions.")]
        public bool IgnoreOcclusions = false;
        [Tooltip("If checked, transmission will work even if the source/target is not in water.")]
        public bool WorkInAir = false;


        [Tooltip("Should there be secondary messages received depending on the channel shape?")]
        public bool EnableEchoing = true;

        [Tooltip("If an echo happens, how much distance can that echo travel in total compared to max range?")]
        [Range(0f,1f)]
        public float RemainingRangeRatioAfterEcho = 0.5f;

        [Tooltip("Angle in degrees, side-to-side.")]
        [Range(0f,180f)]
        public float BottomFiringForwardOpeningAngle = 120;
        
        [Tooltip("Resolution of bottom firing. This is an exponent, so 1->2 doubles number of rays, so does 2->3. Can not be changed during play.")]
        [Range(0,3)]
        public int BottomFiringResolution = 2;
        
        [Tooltip("The terrain object that we will consider for bottom-echoes. Can be anything with a collider in it.")]
        public GameObject TerrainGO;

        [Tooltip("The tolerance in meters to consider a bottom-echo received. To make up for the fact that we are using rays instead of real sound.")]
        public float BottomEchoTolerance = 1f;

        [Tooltip("Allow multiple echoes from the ground? If true, the first found one will be used. Can improve performance.")]
        public bool SingleGroundEcho = true;

        int terrainColliderID;


        Vector3[] entireSphereVecs;
        Vector3[] bottomFiringVectors;


        [Header("Debug")]
        [Tooltip("Draw debug lines to visualize paths of the signal.")]
        public bool DrawSignalLines = true;
        [Tooltip("Set to true to broadcast a test message once.")]
        public bool testBroadcast = false;


        WaterQueryModel waterModel;
        Transceiver[] allTransceivers;
        public GameObject SelfRobotGO {get; private set;}


        // cant send/receive a million things
        // in one tick, so we queue them up
        Queue<string> sendQueue;
        Queue<StringStamped> receiveQueue;
        

        public void SetSoundVelocity(float vel)
        {
            // should be set by the water volume as needed, similar
            // to water currents and forcepoints
            SoundVelocity = vel;
        }    

        void Awake()
        {
            allTransceivers = FindObjectsByType<Transceiver>(FindObjectsSortMode.None);
            waterModel = FindFirstObjectByType<WaterQueryModel>();
            if(TerrainGO == null)
            {
                Debug.LogWarning("Terrain game object not set, trying to find one myself...");
                TerrainGO = FindFirstObjectByType<Terrain>().gameObject;
            }
            terrainColliderID = TerrainGO.GetComponent<Collider>().GetInstanceID();
            if(terrainColliderID == 0)
            {
                Debug.LogWarning("Terrain collider ID is 0, something is wrong!");
            }

            Icosphere.Create(gameObject, BottomFiringResolution);
            // because the icosphre creates 0-centered sphere, we can use the verts as vectors
            entireSphereVecs = GetComponent<MeshFilter>().mesh.vertices;
            FilterCompleteSphere();

            sendQueue = new Queue<string>();
            receiveQueue = new Queue<StringStamped>();

            SelfRobotGO = Utils.FindParentWithTag(gameObject, "robot", false);
        }

        void OnValidate()
        {
            // so you can play with the opening angle live without doing this every update
            FilterCompleteSphere(); 
        }

        bool HitTargetTx(RaycastHit hit, Transceiver tx)
        {
            var hitRobotGO = Utils.FindParentWithTag(hit.transform.gameObject, "robot", false);
            if(hitRobotGO == null) return false; // not a robot
            if(hitRobotGO == tx.SelfRobotGO) return true; // hit something that belongs to the target tx
            return false; // hit another robot lol
        }


        void FilterCompleteSphere()
        {
            if(entireSphereVecs == null) return;

            var selectedVecs = new List<Vector3>();
            for (int i = 0; i < entireSphereVecs.Length; i++)
            {
                // filter out the ones we wont need for bottom echoes
                // effectively we want the ones looking downwards and "forward" 
                // We COULD also do the bottom-echoes not per-target but per-source
                // effectively firing a larger number of rays to the bottom for _all_ targets
                // instead of a smaller number of rays for _each_ target. However i feel
                // that 95% of users will have _maybe_ one or _two_ targets in if any. So
                // the per-target approach is likely to be more relevant.
                Vector3 vec = entireSphereVecs[i];
                if(vec.y > 0) continue; // looking up
                if(vec.z < 0) continue; // looking backwards
                if(Vector3.Angle(Vector3.forward, Vector3.ProjectOnPlane(vec, Vector3.up)) > BottomFiringForwardOpeningAngle/2) continue;
                selectedVecs.Add(vec);
            }
            bottomFiringVectors = selectedVecs.ToArray();
        }

        bool FrontSphereCast(Vector3 from, Vector3 to, out RaycastHit hit, float maxDist = Mathf.Infinity)
        {
            if(IgnoreOcclusions){
                hit = new RaycastHit();
                return false;
            }

            // SphereCast puts the center of the sphere at the start position
            // but want it to start/end a channel-radius away to avoid hitting things
            // that are behind the source/target
            Vector3 posDiffVec = to-from;
            float dist = posDiffVec.magnitude;
            Vector3 direction = posDiffVec.normalized;
            Vector3 startPos = from + MinChannelRadius * direction;
            float castDist = Mathf.Min(maxDist, dist) - MinChannelRadius;
            return Physics.SphereCast(startPos, MinChannelRadius, direction, out hit, castDist);
        }


        void TransmitDirectPath(string data, Transceiver tx)
        {
            RaycastHit hit;
            if(FrontSphereCast(transform.position, tx.transform.position, out hit))
            {
                // ignore the body that ONLY the target tx is attached to
                if(!HitTargetTx(hit, tx))
                {
                    // There is no open corridor of given radius between objects.
                    if(DrawSignalLines) Debug.DrawLine(transform.position, hit.point, Color.red, 1);
                    return;
                }
                // we hit the body of the tx, continue with transmission
            }
            
            // There is an unobstructed corridor
            float delay = Vector3.Distance(transform.position, tx.transform.position) / SoundVelocity;
            StartCoroutine(TransmitWithDelay(data, tx, delay));
            if(DrawSignalLines) Debug.DrawLine(transform.position, tx.transform.position, Color.green, 0.1f);
            
        }

        void TransmitSurfaceEcho(string data, Transceiver tx)
        {
            // To create an echo from the water surface, we need to
            // find out _where_ on the surface the refleciton would occur.
            // To do this efficiently
            // - we assume the surface is flat
            // - reflect the transmitter around the flat plane
            // -- send a raycast from the reflection towards the target
            // -- ignore the hits on the water surface
            // -- if there are no other hits, the water has reflected the signal
            // - use the hit on the water surface to find the distance the signal travels
            // etc.
            //
            // 
            //                  X selfReflectionPos
            //                  |\
            //                  | \
            // water plane ~~~~~~~~X~~~~~~~~ X=echoPoint
            //                  | / \
            //                  |/   \
            //          selfPos O     T targetPos 

            // this reflection could be generalized to _any plane_ rather
            // than the xz-plane we use here with little effort if needed.
            float waterSurfaceLevel = waterModel.GetWaterLevelAt(transform.position);

            // early quit for too-deep-to-reflect-from-surface case
            // the shortest possible reflection distance is if sound
            // bounces perpendicular to water and is received by
            // an acoustically transparent receiver at the surface
            float selfDepth = waterSurfaceLevel - transform.position.y;
            if(selfDepth > MaxRange) return; 

            Vector3 selfPos = transform.position;
            Vector3 selfReflectionPos = new Vector3(
                selfPos.x,
                -selfPos.y + waterSurfaceLevel,
                selfPos.z
            );

            Vector3 targetPos = tx.transform.position;

            // check if the total length of the bounced ray would be within range
            // before we do anything more
            // for that, we need to know where the bounce happens.
            // it happens at the intersection of the water plane and reflection-target line
            Plane waterPlane = new Plane(Vector3.up, new Vector3(0f, waterSurfaceLevel, 0f));
            float selfToReflectionPointDistance = 0f;
            float remainingRangeAfterEcho = MaxRange*RemainingRangeRatioAfterEcho - selfToReflectionPointDistance;
            Ray reflectionToTargetRay = new Ray(selfReflectionPos, targetPos - selfReflectionPos);
            if(waterPlane.Raycast(reflectionToTargetRay, out selfToReflectionPointDistance))
            {
                if(selfToReflectionPointDistance > MaxRange) return; // source too far to reflect
                // re-calc now that we know the distance
                remainingRangeAfterEcho = MaxRange*RemainingRangeRatioAfterEcho - selfToReflectionPointDistance;
                if(remainingRangeAfterEcho <= 0) return; // there wouldnt be enough power left after the echo
            }
            else
            {
                // no intersection means either tx is _on_ the surface or self is on the surface
                // either way comms break
                return;
            }

            // okay, finally we know an echo CAN happen, all distances checked out
            // now lets find exactly where it will happen so we can
            // check for occlusions.
            // we know how far from the reflection point its going to be
            // and we know its on the vector reflection->target
            Vector3 echoPoint = reflectionToTargetRay.GetPoint(selfToReflectionPointDistance);
            // Debug.DrawRay(echoPoint, Vector3.up * 10, Color.magenta, 1);
            
            // found the point on the surface where the echo will happen
            // now we check from source to surface for occlusions

            RaycastHit sourceToSurfaceHit;
            if(FrontSphereCast(selfPos, echoPoint, out sourceToSurfaceHit, MaxRange))
            {
                // there is a hit, no echo
                if(DrawSignalLines) Debug.DrawLine(selfPos, sourceToSurfaceHit.point, Color.red, 1);
                return;
            } 
            if(DrawSignalLines) Debug.DrawLine(selfPos, echoPoint, Color.green, 0.1f);

            // source can reach surface, can the surface reach target?

            RaycastHit surfaceToTargetHit;
            if(FrontSphereCast(echoPoint, tx.transform.position, out surfaceToTargetHit, remainingRangeAfterEcho))
            {
                // ignore the body that ONLY the target tx is attached to
                if(!HitTargetTx(surfaceToTargetHit, tx))
                {
                    // there is a hit, no echo
                    if(DrawSignalLines) Debug.DrawLine(echoPoint, surfaceToTargetHit.point, Color.red, 0.1f);
                    return;
                }
            }
            if(DrawSignalLines) Debug.DrawLine(echoPoint, targetPos, Color.green, 0.1f);
            
            // We got the entire path cleared.
            // Time to transmit!
            // TODO could also add in receiver pick-up characteristics here!
            float totalDistanceTraveled = Vector3.Distance(selfPos, echoPoint) + Vector3.Distance(targetPos, echoPoint);
            float delay = totalDistanceTraveled / SoundVelocity;
            StartCoroutine(TransmitWithDelay(data, tx, delay));

        }

        void TransmitBottomEcho(string data, Transceiver tx)
        {
            // bottom is problematic, because we cant in good conciense assume its flat like the surface
            // thus we use the shotgun approach:
            // - fire a bunch of rays towards the target and bottom
            // -- let them reflect once from only the bottom
            // -- hope one hits the target
            // -- if it does, transmit

            // we need to rotate the sphere slice of rays towards the target
            Vector3 toTarget = tx.transform.position - transform.position;
            toTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            // our default rays are looking forward on the xz plane
            float yawAngle = Vector3.SignedAngle(Vector3.forward, toTarget, Vector3.up);

            Vector3 targetPos = tx.transform.position;

            // fire some spheres, pew pew pew
            // TODO parallelize this like the sensor sonar?
            for (var i = 0; i < bottomFiringVectors.Length; i++)
            {
                Vector3 towardsBottomDirection = Quaternion.Euler(0, yawAngle, 0) * bottomFiringVectors[i];
                // cast towards the ground
                RaycastHit groundHit;
                bool hitTheGround = false;
                if(Physics.SphereCast(transform.position, MinChannelRadius, towardsBottomDirection, out groundHit, MaxRange))
                {
                    if(groundHit.colliderInstanceID == terrainColliderID) hitTheGround = true;
                }
                // check if this is the ground or some other object.
                // if its anything but the ground, no reflection
                if(!hitTheGround) continue;

                // okay, path to ground clear
                // now reflect it
                // we do something fun here:
                // since the raycasts are a very meh approximation of sound
                // it is quite unlikely that any of them will ever hit the target.
                // thus, we cast once for occlusion (small radius) and one for tolerance (big radius, only with target's xz plane)
                
                // first reduce range
                float remainingRangeAfterEcho = MaxRange*RemainingRangeRatioAfterEcho - groundHit.distance;
                if(remainingRangeAfterEcho <= 0) continue; // echo too weak

                bool hitTarget = false;
                Vector3 targetHitPoint = Vector3.zero;
                RaycastHit occlusionHit;
                // reflect the ray we sent to the ground around the normal of the ground
                Vector3 echoDirection = Vector3.Reflect(towardsBottomDirection, groundHit.normal);
                if(DrawSignalLines) Debug.DrawRay(groundHit.point, 3*groundHit.normal, Color.magenta, 0.1f);

                if(Physics.SphereCast(groundHit.point, MinChannelRadius, echoDirection, out occlusionHit, remainingRangeAfterEcho))
                {
                    // hit something that isnt the target, abort
                    if(!HitTargetTx(occlusionHit, tx)) continue;
                    // hit the target tx
                    targetHitPoint = occlusionHit.point; // if the small one hit the target, we can skip the large spherecast :D
                    hitTarget = true;
                }

                if(!hitTarget)
                {
                    // okay, occlusion is clear, but it didnt hit the target either
                    // so we cast a ray towards the target's xz-plane to enable some error in reflections
                    Plane targetPlane = new Plane(Vector3.up, targetPos);
                    Ray echoRay = new Ray(groundHit.point, echoDirection);
                    float groundToTargetDistance;
                    if(targetPlane.Raycast(echoRay, out groundToTargetDistance))
                    {
                        // hit the plane!
                        // get the point
                        Vector3 targetPlaneHitPoint = echoRay.GetPoint(groundToTargetDistance);
                        // check its distance to target
                        // TODO could also use a pick-up pattern of the receiver here... HMM.
                        float echoDistanceFromTarget = Vector3.Distance(targetPlaneHitPoint, targetPos);
                        // we "hit" the target
                        if(echoDistanceFromTarget > BottomEchoTolerance) continue; // not within tolerance, abort this ray
                        else
                        {
                            targetHitPoint = targetPlaneHitPoint;
                            hitTarget = true;
                        }
                    }
                }
                
                if(DrawSignalLines)
                {
                    Color c = Color.red;
                    if(hitTarget) c = Color.blue;
                    Debug.DrawLine(transform.position, groundHit.point, c, 0.1f);
                    Debug.DrawRay(groundHit.point, 5*echoDirection, Color.red, 0.1f);
                }

                if(!hitTarget) continue;

                // finally:
                // - we have checked occlusion, its clear
                // - the occlusion ray didnt hit the target, but it got _close enough_
                // transmit!

                float totalDistanceTraveled = groundHit.distance + Vector3.Distance(groundHit.point, targetPos);
                float delay = totalDistanceTraveled / SoundVelocity;
                StartCoroutine(TransmitWithDelay(data, tx, delay));
                if(DrawSignalLines)
                {
                    Debug.DrawLine(groundHit.point, targetHitPoint, Color.blue, 0.1f);
                }

                // if we only want _one_ ground echo, we found it. be done, stop casting.
                if(SingleGroundEcho) return;

            }
        }
        
        void Broadcast(string data)
        {
            if(!WorkInAir)
            {
                // Doesnt work out of water :(
                float selfWaterSurfaceLevel = waterModel.GetWaterLevelAt(transform.position);
                float selfDepth = selfWaterSurfaceLevel - transform.position.y;
                if(selfDepth < 0) return; // not in water
            }

            // TODO this could be parallelized
            foreach(Transceiver tx in allTransceivers)
            {
                var id = tx.GetInstanceID();
                if(id == this.GetInstanceID()) continue; // skip self

                var dist = Vector3.Distance(transform.position, tx.transform.position);
                if(dist > MaxRange) continue; // skip too far

                if(!tx.WorkInAir)
                {
                    float txWaterSurfaceLevel = waterModel.GetWaterLevelAt(tx.transform.position);
                    float txDepth = txWaterSurfaceLevel - tx.transform.position.y;
                    if(txDepth < 0) continue; // skip not-in-water
                }


                TransmitDirectPath(data, tx);

                // if an echo is to happen...
                if(EnableEchoing)
                {
                    // Things considered for echo:
                    // - Do one large sphereCastAll, look at all the normals and decide if any would reflect towards target
                    // - Do many CastAlls at different radii
                        // ^ anything with a sphereCast will basically never find a hit such that the normal of the surface
                        // is perpendicualar to the A-to-B vector precisely because it is a sphere moving along that vector,
                        // meaning there is exactly one point on that sphere that could hit such a surface.
                        // Thus sphereCast can never find echo-producing surfaces if it is cast towards the target.
                    //
                    // - Shotgun a million rays/spheres
                        // ^ No need to actually bounce the ray, just finding the normal of the hit is enough to calculate
                        // if it will bounce towards the target.
                        // Possibly can be done similar to MBES/SSS in terms of parallelism.
                        // These pings are way less frequently done than the sensors, but require way more rays due to being 2D rather than 1D.
                        // However, given that it will still be based on luck (the determinstically cast rays must hit few specific points)
                        // and that 99% of the time it will be disabled (because most modems already have echo-denial) I will not do this and
                        // simply approximate the chances of it happening given the unobstructed channel shape.
                    //
                    // - Cast ray to surface/bottom
                        // ^ HDRP surface cant have collider to hit.
                        // Standard surface is a plane. Use the same plane in all: AcousticReflectorPlane?
                        // Cheap to calculate the point of inflection and then do a cast to it and from it
                        // to see if its occluded.
                        // Bottom is HARD. Cant assume flat, annoying to simplify...
                    //
                    // - Combination
                    // -- Sphere cast in the column towards target to check occlusion only.
                    // -- Two Casts to/from reflection point on planar water surface.
                    // -- Shotgun rays to bottom and spheres from reflection points towards target
                    // ^ Doable, accurate enough, accounts for MOST of the reflections. gg.

                    TransmitSurfaceEcho(data, tx);
                    TransmitBottomEcho(data, tx);
                }
            }
        }

        IEnumerator TransmitWithDelay(string data, Transceiver tx, float delay)
        {
            StringStamped dp = new StringStamped(data, Clock.NowTimeInSeconds);
            yield return new WaitForSeconds(delay);
            dp.Received(dp.TimeSent + delay);
            tx.Receive(dp);
        }

        void Receive(StringStamped data)
        {
            receiveQueue.Enqueue(data);
        }

        void FixedUpdate()
        {
            if(testBroadcast)
            {
                Write("Test broadcast from " + name);
                testBroadcast = false;
            }
            // TODO tie this to some frequency as well.
            // modems usually have a limit, as a function of
            // data size
            if(sendQueue.Count > 0)
            {
                string data = sendQueue.Dequeue();
                Broadcast(data);
            }
        }

        public StringStamped Read()
        {
            if(receiveQueue.Count > 0) return receiveQueue.Dequeue();
            else return null;
        }

        public void Write(string data)
        {
            // TODO limit size? split?
            sendQueue.Enqueue(data);
        }

        public bool HasNewData()
        {
            return receiveQueue.Count > 0;
        }
    }
}