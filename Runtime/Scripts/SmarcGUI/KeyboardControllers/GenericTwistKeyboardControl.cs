using UnityEngine;
using UnityEngine.InputSystem;
using Smarc.GenericControllers;
using VehicleComponents.Actuators;

namespace SmarcGUI.KeyboardControllers
{
    [RequireComponent(typeof(IGenericTwistActuator))]
    public class GenericTwistKeyboardController : KeyboardControllerBase
    {
        public Vector3 TargetLinearVelocity = Vector3.zero;
        public Vector3 TargetAngularVelocity = Vector3.zero;
        
        public float LinearSpeed = 2f;
        public float AngularSpeed = 15f;


        public float BoostMultiplier = 5f;


        InputAction LeftStick, RightStick, LB;

        IGenericTwistActuator[] twistActuators;
        

        public override void OnReset()
        {
            foreach(var actuator in twistActuators)
            {
                actuator.SetTwist(Vector3.zero, Vector3.zero);
            }
        }

        void Awake()
        {
            LeftStick = InputSystem.actions.FindAction("GamePad/LeftStick");
            RightStick = InputSystem.actions.FindAction("GamePad/RightStick");
            LB = InputSystem.actions.FindAction("GamePad/LB");
            twistActuators = GetComponents<IGenericTwistActuator>();
        }

        void Update()
        {
            var leftStickValue = LeftStick.ReadValue<Vector2>();
            var rightStickValue = RightStick.ReadValue<Vector2>();
            var lbValue = LB.ReadValue<float>();


            TargetLinearVelocity = new Vector3(leftStickValue.x, 0f, leftStickValue.y) * LinearSpeed;
            TargetAngularVelocity = new Vector3(0f, rightStickValue.x, 0f) * AngularSpeed;

            if (lbValue > 0f)
            {
                TargetLinearVelocity *= BoostMultiplier;
                TargetAngularVelocity *= BoostMultiplier;
            }
            
            foreach(var actuator in twistActuators)
            {
                actuator.SetTwist(TargetLinearVelocity, TargetAngularVelocity);
            }
        }

    }
}