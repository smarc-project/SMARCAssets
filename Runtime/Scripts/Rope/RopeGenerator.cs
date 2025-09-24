using UnityEngine;
using ROS.Publishers;
using Utils = DefaultNamespace.Utils;

namespace Rope
{
    public class RopeGenerator : MonoBehaviour
    {
        [Header("Prefab of the rope parts")]
        public GameObject RopeLinkPrefab;
        public GameObject BuoyPrefab;
        public Material RopeMaterial;

        [Header("Connected Body")]
        [Tooltip("What should the first link in the rope connect to? rope_link in SAM.")]
        public string VehicleRopeLinkName = "rope_link";
        public  string VehicleBaseLinkName = "base_link";
    

        [Header("Rope parameters")]
        [Tooltip("Diameter of the rope in meters")]
        public float RopeDiameter = 0.01f;
        [Tooltip("How long the entire rope should be. Rounded to SegmentLength. Ignored if this is not the root of the rope.")]
        public float RopeLength = 1f;
        [Tooltip("How heavy is this rope?")]
        public float GramsPerMeter = 0.5f;
        [Tooltip("How heavy is the buoy at the end. Set to 0 for no buoy.")]
        public float BuoyGrams = 0f;

        [Header("Physics stuff")]
        [Tooltip("Diameter of the collision objects for the rope. The bigger the more stable the physics are.")]
        public float RopeCollisionDiameter = 0.1f;
        [Tooltip("How long each segment of the rope will be. Smaller = more realistic but harder to simulate.")]
        [Range(0.01f, 1f)]
        public float SegmentLength = 0.1f;
        [Tooltip("Rope will be replaced by two sticks when its end-to-end distance is this close to RopeLength")]
        [Range(0f, 0.05f)]
        public float RopeTightnessTolerance = 0.02f;

        [Header("Rendering")]
        public Color RopeColor = Color.yellow;
        LineRenderer ropeLineRenderer;

        [Header("Debug")]
        public bool DrawGizmos = false;

        [HideInInspector] public float SegmentMass => GramsPerMeter * 0.001f * SegmentLength;
        [HideInInspector] public int NumSegments => (int)(RopeLength / (SegmentLength+RopeDiameter));
        //All the rope links we generate will go in here
        [HideInInspector] public GameObject RopeContainer;


        [HideInInspector] public GameObject VehicleRopeLink;
        [HideInInspector] public GameObject VehicleBaseLink;
        readonly string containerName = "Rope";

        

        void OnValidate()
        {
            if(NumSegments > 50) Debug.LogWarning($"There will be {NumSegments} rope segments generated on game Start, might be too many?");
        }

        GameObject InstantiateLink(Transform prevLink, int num)
        {
            var link = Instantiate(RopeLinkPrefab);
            link.transform.SetParent(RopeContainer.transform);
            link.name = $"RopeSegment_{num}";

            var rl = link.GetComponent<RopeLink>();
            rl.SetRopeParams(this);

            if(prevLink != null) rl.SetupConnectionToPrevLink(prevLink);
            else rl.SetupConnectionToVehicle(VehicleRopeLink, VehicleBaseLink);
            
            return link;
        }

        void InstantiateAllLinks()
        {
            var prevLink = InstantiateLink(null, 0);

            for(int i=1; i < NumSegments; i++)
                prevLink = InstantiateLink(prevLink.transform, i);

            foreach(RopeLink rl in RopeContainer.GetComponentsInChildren<RopeLink>())
                rl.AssignFirstAndLastSegments();
        }

        void CreateLineRenderer()
        {
            ropeLineRenderer = RopeContainer.AddComponent<LineRenderer>();
            ropeLineRenderer.material = RopeMaterial;
            ropeLineRenderer.startColor = RopeColor;
            ropeLineRenderer.endColor = RopeColor;
            ropeLineRenderer.startWidth = RopeDiameter;
            ropeLineRenderer.endWidth = RopeDiameter;
            ropeLineRenderer.positionCount = NumSegments + 1;
            ropeLineRenderer.useWorldSpace = true;
            ropeLineRenderer.receiveShadows = true;
            ropeLineRenderer.generateLightingData = true;
        }


        void InstantiateBuoy()
        {
            var lastLink = RopeContainer.transform.GetChild(NumSegments-1);
            var lastLinkRL = lastLink.GetComponent<RopeLink>();
            var (lastLinkFront, _) = lastLinkRL.SpherePositions();
            var buoy = Instantiate(BuoyPrefab);
            buoy.transform.SetParent(RopeContainer.transform);
            buoy.transform.SetPositionAndRotation(lastLink.position + lastLinkFront, lastLink.rotation);
            buoy.name = "Buoy";
            var buoyRB = buoy.GetComponent<Rigidbody>();
            buoyRB.mass = BuoyGrams * 0.001f;
            var buoyJoint = buoy.GetComponent<CharacterJoint>();
            buoyJoint.connectedBody = lastLink.GetComponent<Rigidbody>();
            var RLBuoy = buoy.GetComponent<RopeLinkBuoy>();
            RLBuoy.OtherSideOfTheRope = VehicleRopeLink.GetComponent<ArticulationBody>();
        }


        public void SpawnRope()
        {
            if(!gameObject.CompareTag("robot"))
            {
                Debug.LogError("RopeGenerator: No vehicle found. Make sure this is a child of a vehicle tagged 'robot'.");
                return;
            }
            
            VehicleRopeLink = Utils.FindDeepChildWithName(gameObject, VehicleRopeLinkName);
            VehicleBaseLink = Utils.FindDeepChildWithName(gameObject, VehicleBaseLinkName);
            
            RopeContainer = Utils.FindDeepChildWithName(gameObject, containerName);
            if (RopeContainer == null)
            {
                RopeContainer = new GameObject(containerName);
                RopeContainer.transform.SetParent(gameObject.transform);
                RopeContainer.transform.SetPositionAndRotation(VehicleRopeLink.transform.position, VehicleRopeLink.transform.rotation);
                var ropetf = RopeContainer.AddComponent<ROSTransformTreePublisher>();
                ropetf.NotARobot = true;
                ropetf.SetBaseLinkName(RopeContainer.name);
            }

            InstantiateAllLinks();
            CreateLineRenderer();
            UpdateLineRenderer();
            if(BuoyGrams > 0 && BuoyPrefab != null) InstantiateBuoy();

        }

        void DestroyEitherWay(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        public void DestroyRope(bool keepBuoy = false)
        {
            if(!gameObject.CompareTag("robot"))
            {
                Debug.LogError("RopeGenerator: No vehicle found. Make sure this is a child of a vehicle tagged 'robot'.");
                return;
            }

            RopeContainer = Utils.FindDeepChildWithName(gameObject, containerName);
            if(RopeContainer == null)
            {
                Debug.LogError("RopeGenerator: No rope found, cannot destroy rope.");
                return;
            }

            if(!keepBuoy) DestroyEitherWay(RopeContainer);
            else 
            {
                foreach(RopeLink rl in RopeContainer.GetComponentsInChildren<RopeLink>())
                {
                    DestroyEitherWay(rl.gameObject);
                }
                Destroy(ropeLineRenderer);
                ropeLineRenderer = null;
            }
        }


        void Awake()
        {
            if(!gameObject.CompareTag("robot"))
            {
                Debug.LogError("RopeGenerator: No vehicle found. Make sure this is a child of a vehicle tagged 'robot'.");
                return;
            }
            if(RopeContainer == null) RopeContainer = Utils.FindDeepChildWithName(gameObject, containerName);
            if(VehicleRopeLink == null) VehicleRopeLink = Utils.FindDeepChildWithName(gameObject, VehicleRopeLinkName);
            if(VehicleBaseLink == null) VehicleBaseLink = Utils.FindDeepChildWithName(gameObject, VehicleBaseLinkName);
            if(RopeContainer != null && ropeLineRenderer == null) ropeLineRenderer = RopeContainer.GetComponent<LineRenderer>();
        }


        void UpdateLineRenderer()
        {
            if(ropeLineRenderer == null) return;
            var ropeLinks = RopeContainer.GetComponentsInChildren<RopeLink>();
            ropeLineRenderer.positionCount = ropeLinks.Length+1;
            foreach(var rl in ropeLinks)
                for(int i=0; i<ropeLinks.Length; i++)
                {
                    ropeLineRenderer.SetPosition(i, ropeLinks[i].transform.position);
                }

            var lastChild = ropeLinks[^1].transform;
            ropeLineRenderer.SetPosition(NumSegments, lastChild.position+lastChild.forward*SegmentLength);
        }


        void Update()
        {
            if(RopeContainer != null) UpdateLineRenderer();
        }

    }
}
