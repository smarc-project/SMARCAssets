using DefaultNamespace;
using UnityEngine;
using VehicleComponents.ROS.Publishers; // Needed for TF publishing


namespace Rope
{
    [RequireComponent(typeof(Collider))]
    public class RopeHook : MonoBehaviour
    {

        [Header ("Rope System")]
        [Tooltip("The winch that the hook is attached to.")]
        public GameObject WinchGO;
        [Tooltip("The pulley that is attached to the hook.")]
        public GameObject PulleyGO;


        [Header("TF Settings")]
        [Tooltip("TF frame name for the hook.")]
        public string HookTFFrame = "Hook";

        [Header("Debug")]
        public bool DrawForces = false;

        [Tooltip("If true, the hook will attach to the base_link of the given buoy on start.")]
        public bool AttachToRopeLinkAfterStart = false;
        public RopeLinkBuoy RopeLinkBuoy;

<<<<<<< HEAD
        void Start()
        {
            // Add TF publisher for the hook
            var tfPub = gameObject.AddComponent<ROSTransformTreePublisher>();
            tfPub.BaseLinkName = HookTFFrame; // "Hook"
            Debug.Log($"Hook TF frame set: {HookTFFrame}");
        }

=======
        bool connectedToBuoy = false;
>>>>>>> upstream/master

        void FixedUpdate()
        {
            if(AttachToRopeLinkAfterStart && RopeLinkBuoy != null)
            {
                var theRobot = Utils.FindParentWithTag(RopeLinkBuoy.OtherSideOfTheRope.gameObject, "robot", false);
                var theRope = theRobot.transform.Find("Rope");
                AttachDroneToRopeLink(RopeLinkBuoy);
                // clean up.
                Destroy(theRope.gameObject);
                Destroy(PulleyGO);
                Destroy(gameObject);
            }
        }

        void AttachDroneToRopeLink(RopeLinkBuoy rlb)
        {
            // if we collide with the buoy, that means the pulley is tight
            // for sim stability, we will destroy the pulley, the buoy and the hook
            // and attach the "OtherSideOfTheRope" to the winch directly.
            var winch = WinchGO.GetComponent<Winch>();
            winch.UnSetup();
            // this could be generalized to RBs and such... but for now, we'll just do the ArticulationBody
            winch.LoadAB = rlb.OtherSideOfTheRope;
            winch.CurrentRopeSpeed = 0;
            winch.WinchSpeed = 0;
            var dist = Vector3.Distance(rlb.OtherSideOfTheRope.transform.position, winch.transform.position);
            winch.TargetLength = dist;
            winch.CurrentLength = dist;
            winch.Setup();
        }

        void OnCollisionStay(Collision collision)
        {
            if (!connectedToBuoy && collision.gameObject.TryGetComponent(out RopeLinkBuoy rlb))
            {
                var jointToBuoy = gameObject.AddComponent<CharacterJoint>();
                jointToBuoy.connectedBody = rlb.GetComponent<Rigidbody>();
                connectedToBuoy = true;
            }
        }

    }
}