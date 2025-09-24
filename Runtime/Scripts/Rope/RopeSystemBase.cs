using UnityEngine;
using System.Collections.Generic;

using Force;


namespace Rope
{
    public class RopeSystemBase : MonoBehaviour
    {

        [Header("Rope Properties")]
        public float RopeDiameter = 0.1f;
        [Tooltip("Length of the rope. A winch can extend this much and a Pulley can have this distance between the two ends.")]
        public float RopeLength = 5f;

        [Tooltip("Set false if you want to call Setup() manually, maybe as a result of a button press or event.")]
        public bool SetupOnStart = true;

        List<GameObject> ropeParts;
        protected bool setup = false;


        void Start()
        {
            if(!SetupOnStart) return;
            Setup();
        }

        public void Setup()
        {
            var selfRB = GetComponent<Rigidbody>();
            if(selfRB == null) AddIneffectiveRB(gameObject);
            SetupOnStart = false;
            if(ropeParts == null) ropeParts = new List<GameObject>();
            SetupEnds();
            setup = true;
        }
        public void UnSetup()
        {
            foreach (var connector in ropeParts)
            {
                if (Application.isPlaying) Destroy(connector);
                else DestroyImmediate(connector);
            }
            setup = false;
        }


        protected virtual void SetupEnds()
        {
            Debug.LogWarning("SetupEnds() not implemented in " + GetType());
        }


        protected static Rigidbody AddIneffectiveRB(GameObject o)
        {
            Rigidbody rb = o.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.inertiaTensor = Vector3.one * 1e-6f;
            rb.linearDamping = 1f;
            rb.angularDamping = 1f;
            rb.mass = 0.1f;
            return rb;
        }



        protected SpringJoint AttachBody(MixedBody load)
        {
            Rigidbody baseRB = GetComponent<Rigidbody>();

            GameObject rope = new($"Rope_{transform.name}--{load.transform.name}");
            ropeParts.Add(rope);
            rope.transform.parent = transform.parent;
            rope.transform.SetPositionAndRotation(transform.position, transform.rotation);
            var ropeRB = AddIneffectiveRB(rope);
            

            var ropeJoint = rope.AddComponent<SpringJoint>();
            ropeJoint.enablePreprocessing = false;
            ropeJoint.enableCollision = false;
            ropeJoint.autoConfigureConnectedAnchor = true;
            ropeJoint.connectedBody = baseRB;
            // ropeJoint.anchor = Vector3.zero;
            // ropeJoint.connectedAnchor = Vector3.zero;
            ropeJoint.spring = 5000;
            ropeJoint.damper = 500;
            ropeJoint.maxDistance = RopeLength;


            var loadConnector = new GameObject($"Connector_{transform.name}--{load.transform.name}");
            ropeParts.Add(loadConnector);
            loadConnector.transform.parent = transform.parent;
            loadConnector.transform.SetPositionAndRotation(load.position, load.rotation);
            var loadConRB = AddIneffectiveRB(loadConnector);
            
            var connectorToRopeJoint = loadConnector.AddComponent<FixedJoint>();
            connectorToRopeJoint.connectedBody = ropeRB;
            connectorToRopeJoint.autoConfigureConnectedAnchor = true;
            // connectorToRopeJoint.anchor = Vector3.zero;
            // connectorToRopeJoint.connectedAnchor = Vector3.zero;

            var loadJoint = loadConnector.AddComponent<CharacterJoint>();
            loadJoint.enablePreprocessing = false;
            loadJoint.enableCollision = false;
            // loadJoint.anchor = Vector3.zero;
            loadJoint.autoConfigureConnectedAnchor = false;
            load.ConnectToJoint(loadJoint);
            loadJoint.connectedAnchor = Vector3.zero;

            // Add a linerenderer to visualize the rope and its tight/slack state
            var lr = rope.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startWidth = RopeDiameter;
            lr.startColor = Color.blue;
            lr.endColor = lr.startColor;
            lr.SetPosition(0, transform.position);
            lr.SetPosition(1, load.position);

            return ropeJoint;
        }
    }
}