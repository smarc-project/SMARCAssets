using System;
using UnityEngine;

namespace VehicleComponents.Actuators
{
    public class VBS : LinkAttachment, IPercentageActuator
    {
        [Header("VBS")] [Range(0, 100)] public float percentage = 50f;

        [Range(0, 100)] public float resetValue = 50f;

        public float maxVolume_l = 0.250f;
        public float density = 997f; //kg/m3

        private float _initialMass;
        private float _maximumPos;
        private float _minimumPos;

        public new void Awake()
        {
            base.Awake();
            //TODO: VBS Starts at 5% in the real world.
            var xDrive = parentMixedBody.xDrive;
            //   _initialMass = parentArticulationBody.mass;
            _initialMass = density / 1000 * maxVolume_l;
            _minimumPos = xDrive.upperLimit;
            _maximumPos = xDrive.lowerLimit;
        }

        public void SetPercentage(float newValue)
        {
            percentage = Mathf.Clamp(newValue, 0, 100);
        }

        public float GetResetValue()
        {
            return resetValue;
        }

        public float GetCurrentValue()
        {
            return (1 - (mixedBody.jointPosition[0] - _minimumPos) / (_maximumPos - _minimumPos)) * 100;
        }

        public bool HasNewData()
        {
            return true;
        }

        new public void FixedUpdate()
        {
            base.FixedUpdate();
            if (Physics.simulationMode == SimulationMode.FixedUpdate) DoUpdate();
        }

        public void DoUpdate()
        {
            mixedBody.mass = 0.300f + _initialMass * GetCurrentValue() / 100; // Piston weight + water weight
            var computeTargetValue = ComputeTargetValue(percentage);
            mixedBody.SetDriveTarget(ArticulationDriveAxis.X, computeTargetValue);
        }

        public float ComputeTargetValue(float target)
        {
            return Mathf.Lerp(_maximumPos, _minimumPos, target / 100);
        }
    }
}