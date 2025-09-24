using UnityEngine;
using UnityEngine.InputSystem;

using dji;

namespace SmarcGUI.KeyboardControllers
{
    [RequireComponent(typeof(DJIController))]
    public class DJIKeyboardController : KeyboardControllerBase
    {
        InputAction forwardAction, strafeAction, verticalAction;
        public float maxCmdVelocity = 1.0f;
        public float maxCmdPosition = 1.0f;

        DJIController dji;

        void Awake()
        {
            forwardAction = InputSystem.actions.FindAction("Robot/Forward");
            strafeAction = InputSystem.actions.FindAction("Robot/Strafe");
            verticalAction = InputSystem.actions.FindAction("Robot/UpDown");
            dji = GetComponent<DJIController>();
        }

        void Update()
        {
            var forwardValue = forwardAction.ReadValue<float>();
            var strafeValue = -strafeAction.ReadValue<float>();
            var verticalValue = verticalAction.ReadValue<float>();

            if (dji.MotorsOff && verticalValue > 0)
            {
                dji.MotorsOff = false;
                dji.TakeOff();
            }
            
            if(dji.IsTakingOff || dji.IsLanding){
                return;
            }

            // FLU and ENU are aligned when X is forward and East, so no need to transform the values here.
            // the controller should be responsible for transforming the values wrt to pose of drone.
            var max = dji.ControllerType == ControllerType.FLU_Velocity ? maxCmdVelocity : maxCmdPosition;
            var cmd = new Vector3(forwardValue, strafeValue, verticalValue);
            dji.CommandVelocityFLU = Vector3.ClampMagnitude(cmd, max);
        }

        public override void OnReset()
        {
            dji.CommandVelocityFLU = Vector3.zero;
            dji.CommandPositionENU = Vector3.zero;
        }

    }
}