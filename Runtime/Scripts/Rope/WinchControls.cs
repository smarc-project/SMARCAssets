using UnityEngine;

namespace Rope
{
    public class WinchControl : MonoBehaviour
    {
        [Header("Winch controls")]
        [Range(0f, 10f)]
        public float TargetLength = 0.5f;
        [Range(0f, 5f)]
        public float WinchSpeed = 0.5f;

        [Header("Components")]
        public GameObject WinchGO;
        Winch winch;
        FixedJoint winchJointToVehicle;
        public GameObject HookGO;
        RopeHook hook;

        [Header("Attachment point to vehicle")]
        public ArticulationBody AttachmentAB;
        public Rigidbody AttachmentRB;

        [Header("Rope properties")]
        public float RopeLength;
        public float RopeDiameter;
        public Material WinchRopeMaterial;



        void Start()
        {
            if (AttachmentAB == null && AttachmentRB == null)
            {
                Debug.LogError("No attachment body set on winch controls. Please set either an ArticulationBody or Rigidbody to attach the winch rope to.");
                gameObject.SetActive(false);
                WinchGO.SetActive(false);
                HookGO.SetActive(false);
                return;
            }

            if (WinchGO != null) winch = WinchGO.GetComponent<Winch>();
            if (HookGO != null) hook = HookGO.GetComponent<RopeHook>();

            if (winch == null && hook == null)
            {
                Debug.LogError("No winch or hook component found on the assigned game objects. Please assign ALL.");
                gameObject.SetActive(false);
                WinchGO.SetActive(false);
                HookGO.SetActive(false);
                return;
            }

            winchJointToVehicle = WinchGO.GetComponent<FixedJoint>();
            if (winchJointToVehicle == null)
            {
                Debug.LogError("No FixedJoint found on the winch game object.");
                gameObject.SetActive(false);
                WinchGO.SetActive(false);
                HookGO.SetActive(false);
                return;
            }

            winchJointToVehicle.connectedBody = AttachmentRB;
            winchJointToVehicle.connectedArticulationBody = AttachmentAB;

            winch.RopeDiameter = RopeDiameter;
            winch.RopeLength = RopeLength;
            winch.RopeMaterial = WinchRopeMaterial;
            
            winch.Setup();
        }

        void FixedUpdate()
        {
            winch.TargetLength = TargetLength;
            winch.WinchSpeed = WinchSpeed;
        }


    }
}