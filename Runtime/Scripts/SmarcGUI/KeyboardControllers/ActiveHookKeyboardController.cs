using UnityEngine;
using UnityEngine.InputSystem;
using Smarc.GenericControllers;

namespace SmarcGUI.KeyboardControllers
{
    [RequireComponent(typeof(HorizontalControllerBase))]
    [RequireComponent(typeof(AltitudeControllerBase))]
    [RequireComponent(typeof(AttitudeControllerBase))]
    public class ActiveHookKeyboardController : KeyboardControllerBase
    {
        public float HorizontalSpeed = 1f;
        public float VerticalSpeed = 1f;
        public float YawSpeed = 10f;
        public float BoostMultiplier = 2f;
        InputAction forwardAction, strafeAction, tvAction, boostAction;

        AltitudeControllerBase altCtrl;
        AttitudeControllerBase attCtrl;
        HorizontalControllerBase horizCtrl;

        void Awake()
        {
            forwardAction = InputSystem.actions.FindAction("Robot/Forward");
            strafeAction = InputSystem.actions.FindAction("Robot/Strafe");
            tvAction = InputSystem.actions.FindAction("Robot/ThrustVector");
            boostAction = InputSystem.actions.FindAction("Robot/Boost");

            altCtrl = GetComponent<AltitudeControllerBase>();
            attCtrl = GetComponent<AttitudeControllerBase>();
            horizCtrl = GetComponent<HorizontalControllerBase>();
        }

        void OnEnable(){}
        void OnDisable(){}
        public override void OnReset()
        {
            horizCtrl.TargetVelocity = Vector3.zero;
            altCtrl.TargetVelocity = 0f;
            attCtrl.TargetYawRate = 0f;
        }

        void Update()
        {
            var forwardValue = forwardAction.ReadValue<float>();
            var strafeValue = strafeAction.ReadValue<float>();
            var tvValue = tvAction.ReadValue<Vector2>();
            var yawValue = tvValue.x;
            var verticalValue = tvValue.y;
            var boostValue = boostAction.ReadValue<float>();


            forwardValue *= boostValue > 0 ? BoostMultiplier : 1f;
            strafeValue *= boostValue > 0 ? BoostMultiplier : 1f;
            verticalValue *= boostValue > 0 ? BoostMultiplier : 1f;
            yawValue *= boostValue > 0 ? BoostMultiplier : 1f;

            horizCtrl.TargetVelocity = new Vector3(strafeValue * HorizontalSpeed, 0, forwardValue * HorizontalSpeed);
            altCtrl.TargetVelocity = verticalValue * VerticalSpeed;
            attCtrl.TargetYawRate = yawValue * YawSpeed;
        }


    }
}