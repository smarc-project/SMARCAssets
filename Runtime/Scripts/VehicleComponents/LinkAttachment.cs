using UnityEngine;
using Utils = DefaultNamespace.Utils;
using Force;

namespace VehicleComponents
{
    public class LinkAttachment : MonoBehaviour
    {
        [Header("Link attachment")] 
        [Tooltip("The name of the link the sensor should be attached to.")]
        public string linkName = "";

        [Tooltip("If true, will try on FixedUpdates to attach, if false attach only on Awake")]
        public bool retryUntilSuccess = true;

        [Tooltip("ROS uses a different camera reference frame than Unity, this rotates the Unity camera to match that.")]
        public bool rotateForROSCamera = false;

        [Tooltip("Rotate the object with respect to the attached link after attaching.")]
        public float roll = 0f, pitch = 0f, yaw = 0f;

        [Tooltip("Should the orientation of the object be fixed, even if the link moves?")]
        public bool FixedRotation = false;
        Quaternion initialRotation;

        protected GameObject attachedLink;
        protected ArticulationBody parentArticulationBody;
        protected ArticulationBody articulationBody;
        protected MixedBody mixedBody;
        protected MixedBody parentMixedBody;

        protected void Awake()
        {
            Attach();
        }

        protected void Attach()
        {
            var theRobot = Utils.FindParentWithTag(gameObject, "robot", false);
            if (theRobot == null)
            {
                Debug.Log($"[{transform.name}] No robot found to attach to a part of! Disabling {gameObject.name}.");
                gameObject.SetActive(false);
                return;
            }
            attachedLink = Utils.FindDeepChildWithName(theRobot, linkName);
            if (attachedLink == null)
            {
                Debug.Log($"Object with name [{linkName}] not found under parent [{theRobot.name}]. Disabling {gameObject.name}.");
                gameObject.SetActive(false);
                return;
            }

            transform.SetPositionAndRotation(
                attachedLink.transform.position,
                attachedLink.transform.rotation
            );
            transform.Rotate(Vector3.up, yaw);
            transform.Rotate(Vector3.right, pitch);
            transform.Rotate(Vector3.forward, roll);

            if (rotateForROSCamera)
            {
                transform.Rotate(Vector3.up, 90);
                transform.Rotate(Vector3.right, -90);
                transform.Rotate(Vector3.forward, 180);
            }

            transform.SetParent(attachedLink.transform);
            initialRotation = transform.rotation;

            GetMixedBody();
        }


        public MixedBody GetMixedBody()
        {
            if(mixedBody == null || parentMixedBody == null)
            {
                if (TryGetComponent(out ArticulationBody ab))
                    mixedBody = new MixedBody(ab, null);
                else if (TryGetComponent(out Rigidbody rb))
                    mixedBody = new MixedBody(null, rb);

                if (transform.parent.TryGetComponent(out ArticulationBody parentAB))
                    parentMixedBody = new MixedBody(parentAB, null);
                else if (transform.parent.TryGetComponent(out Rigidbody parentRB))
                    parentMixedBody = new MixedBody(null, parentRB);


                if (mixedBody == null || !mixedBody.isValid) mixedBody = parentMixedBody;
            }
            return mixedBody;
        }


        protected void FixedUpdate()
        {
            if (attachedLink == null && retryUntilSuccess) Attach();

            if (FixedRotation) transform.rotation = initialRotation;
        }

        void OnDrawGizmosSelected()
        {
            // Draw a semitransparent red cube at the transforms position
            Gizmos.color = new Color(1, 0, 0, 0.2f);
            Gizmos.DrawCube(transform.position, new Vector3(0.1f, 0.1f, 0.1f));
        }

        void OnDrawGizmos()
        {
            // Draw a semitransparent green cube at the transforms position
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Gizmos.DrawCube(transform.position, new Vector3(0.1f, 0.1f, 0.1f));
        }
    }
}