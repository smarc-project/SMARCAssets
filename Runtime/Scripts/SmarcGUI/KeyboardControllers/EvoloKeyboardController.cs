using UnityEngine;
using UnityEngine.InputSystem;
using Evolo;
using ROS.Subscribers;

namespace SmarcGUI.KeyboardControllers
{

    [RequireComponent(typeof(EvoloController))]
    //[RequireComponent(typeof(GenericTwistCommand_Sub))]
    [RequireComponent(typeof(EvoloCommand))]
    public class EvoloKeyboardController : KeyboardControllerBase
    {
        InputAction forwardAction, strafeAction, verticalAction;
        EvoloController evoloCtrl;
        EvoloCommand otherctrl;
        bool otherctrlState;

        public float SpeedChangeRate = 0.1f;
        public float YawChangeRate = 0.1f;
        public float AltitudeChangeRate = 0.1f;

        void OnEnable()
        {
            otherctrlState = otherctrl.enabled;
            otherctrl.enabled = false;
        }

        void OnDisable()
        {
            otherctrl.enabled = otherctrlState;
        }



        void Awake()
        {
            forwardAction = InputSystem.actions.FindAction("Robot/Forward");
            strafeAction = InputSystem.actions.FindAction("Robot/Strafe");
            verticalAction = InputSystem.actions.FindAction("Robot/UpDown");
            
            evoloCtrl = GetComponent<EvoloController>();
            otherctrl = GetComponent<EvoloCommand>();
        }

        void Update()
        {
            var forwardValue = forwardAction.ReadValue<float>();
            var strafeValue = strafeAction.ReadValue<float>();
            var verticalValue = verticalAction.ReadValue<float>();

            if (Mathf.Sign(forwardValue) == Mathf.Sign(evoloCtrl.Speed)) evoloCtrl.Speed += SpeedChangeRate * forwardValue;
            else evoloCtrl.Speed = 0;

            evoloCtrl.YawRate += YawChangeRate * strafeValue;
            evoloCtrl.Altitude += verticalValue * AltitudeChangeRate;
        }

        public override void OnReset()
        {
            evoloCtrl.YawRate = 0f;
            evoloCtrl.Speed = 0f;
            evoloCtrl.Altitude = 0f;
        }

    }
}
