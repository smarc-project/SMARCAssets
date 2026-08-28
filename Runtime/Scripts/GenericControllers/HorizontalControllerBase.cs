using UnityEngine;
using Force;
using System;

namespace Smarc.GenericControllers
{
    public enum HorizontalControlMode
    {
        UnityPosition,
        Velocity
    }

    public class HorizontalControllerBase : MonoBehaviour
    {
        [Header("Horizontal Controller Base")]
        public ArticulationBody RobotAB;
        public Rigidbody RobotRB;
        protected MixedBody robotBody;

        public HorizontalControlMode ControlMode = HorizontalControlMode.UnityPosition;
        public float MaxForce = 0f;
        public float MaxSpeed = 5.0f;
        public bool CanMoveSideways = true;


        [Header("Velocity Controller")]
        public Vector3 TargetVelocity = Vector3.zero;

        [Header("Unity Position Controller")]
        public Vector3 TargetUnityPosition = Vector3.zero;
        public float PositionTolerance = 0.5f;

        [Header("Safety")]
        public float MaxUpDot = 0.5f;

        public Vector3 LastAppliedForce { get; private set; }
        public Vector3 LastAppliedForceLocal {get; private set;}

        // Use a generated object to apply force at center of mass, parented to the robot transform
        // This way, we don't have to recalculate the world position of the COM every frame
        protected Transform COM;

        protected void Start()
        {
            robotBody = new MixedBody(RobotAB, RobotRB);
            var globalCom = robotBody.GetTotalConnectedCenterOfMass();
            COM = new GameObject("HorizontalController_COM").transform;
            COM.parent = robotBody.transform;
            COM.position = globalCom;

            // set to current position so it doesnt try to fly away to (usually) origin lol
            if (ControlMode == HorizontalControlMode.UnityPosition)
            {
                TargetUnityPosition = COM.position;
            }
        }

        void FixedUpdate()
        {
            // check if the robot is upright enough to control horizontal movement
            var upDot = Vector3.Dot(robotBody.transform.up, Vector3.up);
            if (upDot < 0.5f)
            {
                Debug.Log($"Robot too tilted for horizontal control! upDot: {upDot}");
                return;
            }

            var currentSpeed = robotBody.localVelocity.magnitude;
            if (currentSpeed > MaxSpeed*10f)
            {
                Debug.Log($"Robot moving WAY too fast for any horizontal control! currentSpeed: {currentSpeed}");
                return;
            }
            if (currentSpeed > MaxSpeed)
            {
                // just a little too fast, we can probably brake it down...
                TargetVelocity = Vector3.zero;
            }

            Vector3 f = GetHorizontalForceLocal();
            f.y = 0;
            if (!CanMoveSideways) f.x = 0;
            LastAppliedForceLocal = f;
            LastAppliedForce = robotBody.transform.TransformVector(f);
            robotBody.AddForceAtPosition(LastAppliedForce, COM.position, ForceMode.Force);
        }


        protected virtual Vector3 GetHorizontalForceLocal()
        {
            throw new NotImplementedException("GetHorizontalForceLocal() must be implemented in a derived class");
        }

    }   
}