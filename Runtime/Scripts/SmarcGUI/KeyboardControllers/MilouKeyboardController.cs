using UnityEngine;
using UnityEngine.InputSystem;
using Smarc.GenericControllers;

namespace SmarcGUI.KeyboardControllers
{
    [RequireComponent(typeof(HorizontalController))]
    [RequireComponent(typeof(AttitudeController))]
    public class MilouKeyboardController : KeyboardControllerBase
    {
        public float Speed = 0.2f;
        public float YawRate = 10f;
        public float BoostMultiplier = 5f;

        public float AutoSpeed = 0f;
        public float AutoYawRate = 0f;

        InputAction forwardAction, strafeAction, boostAction;
        HorizontalController horizontalCtrl;
        AttitudeController attitudeCtrl;

        public override void OnReset()
        {
            horizontalCtrl.TargetVelocity = Vector3.zero;
            attitudeCtrl.TargetYawRate = 0f;
        }

        void Awake()
        {
            forwardAction = InputSystem.actions.FindAction("Robot/Forward");
            strafeAction = InputSystem.actions.FindAction("Robot/Strafe");
            boostAction = InputSystem.actions.FindAction("Robot/Boost");
            
            horizontalCtrl = GetComponent<HorizontalController>();
            attitudeCtrl = GetComponent<AttitudeController>();
        }

        void Update()
        {
            var forwardValue = forwardAction.ReadValue<float>();
            var yawValue = strafeAction.ReadValue<float>();
            var boostValue = boostAction.ReadValue<float>();

            horizontalCtrl.TargetVelocity = new Vector3(0, 0, forwardValue * Speed * (boostValue > 0 ? BoostMultiplier : 1f));
            attitudeCtrl.TargetYawRate = yawValue * YawRate * (boostValue > 0 ? BoostMultiplier : 1f);

            if (AutoSpeed != 0f)
            {
                horizontalCtrl.TargetVelocity = new Vector3(0, 0, AutoSpeed);
            }

            if (AutoYawRate != 0f)
            {
                attitudeCtrl.TargetYawRate = AutoYawRate;
            }

        }

    }
}