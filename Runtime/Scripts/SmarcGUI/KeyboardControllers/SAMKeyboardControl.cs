using UnityEngine;
using UnityEngine.InputSystem;
using Hinge = VehicleComponents.Actuators.Hinge;
using Propeller = VehicleComponents.Actuators.Propeller;
using VBS = VehicleComponents.Actuators.VBS;
using Prismatic = VehicleComponents.Actuators.Prismatic;

namespace SmarcGUI.KeyboardControllers
{
    public class SAMKeyboardControl : KeyboardControllerBase
    {
        public GameObject yawHingeGo;
        public GameObject pitchHingeGo;
        public GameObject frontPropGo;
        public GameObject backPropGo;
        public GameObject vbsGo;
        public GameObject lcgGo;

        Hinge yaw, pitch;
        Propeller frontProp, backProp;
        VBS vbs;
        Prismatic lcg;


        public float rollRpms = 0.1f;
        public float moveRpms = 800f;


        InputAction forwardAction, tvAction, vbsAction, lcgAction, rollAction;


        void Awake()
        {
            yaw = yawHingeGo.GetComponent<Hinge>();
            pitch = pitchHingeGo.GetComponent<Hinge>();
            frontProp = frontPropGo.GetComponent<Propeller>();
            backProp = backPropGo.GetComponent<Propeller>();
            vbs = vbsGo.GetComponent<VBS>();
            lcg = lcgGo.GetComponent<Prismatic>();
        }

        void Start()
        {
            forwardAction = InputSystem.actions.FindAction("Robot/Forward");
            tvAction = InputSystem.actions.FindAction("Robot/ThrustVector");
            vbsAction = InputSystem.actions.FindAction("Robot/UpDown");
            lcgAction = InputSystem.actions.FindAction("Robot/Pitch");
            rollAction = InputSystem.actions.FindAction("Robot/Roll");
        }

        void Update()
        {
            var rpm = forwardAction.ReadValue<float>() * moveRpms;
            var rollValue = rollAction.ReadValue<float>();
            if (rollValue == 0)
            {
                frontProp.SetRpm(rpm);
                backProp.SetRpm(rpm);
            }
            else
            {
                frontProp.SetRpm(rpm * rollValue);
                backProp.SetRpm(rpm * -rollValue);
            }

            var tv = tvAction.ReadValue<Vector2>();
            yaw.SetAngle(tv.x * 0.1f);
            pitch.SetAngle(-tv.y * 0.1f);

            var vbsValue = vbsAction.ReadValue<float>();
            vbs.SetPercentage(100 - ((vbsValue + 1) / 2 * 100));

            var lcgValue = lcgAction.ReadValue<float>();
            lcg.SetPercentage(100 - (lcgValue + 1) / 2 * 100);
        }

        public override void OnReset()
        {
            frontProp.SetRpm(0);
            backProp.SetRpm(0);
            pitch.SetAngle(0);
            yaw.SetAngle(0);
            vbs.SetPercentage(0);
            lcg.SetPercentage(0);
        }
    }
}