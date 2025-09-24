using UnityEngine;

namespace VehicleComponents.Actuators
{
    public class Prismatic : LinkAttachment, IPercentageActuator
    {
        [Header("Position")] [Range(0, 100)] public float percentage = 50f;
        [Range(0, 100)] public float resetValue = 50f;

        private float _maximumPos;
        private float _minimumPos;

        public new void Awake()
        {
            base.Awake();
            var xDrive = GetMixedBody().xDrive;
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

        new public void FixedUpdate()
        {
            base.FixedUpdate();
            if (Physics.simulationMode == SimulationMode.FixedUpdate) DoUpdate();
        }

        public void DoUpdate()
        {
            mixedBody.SetDriveTarget(ArticulationDriveAxis.X, ComputeTargetValue(percentage));
        }

        public bool HasNewData()
        {
            return true;
        }

        public float ComputeTargetValue(float target)
        {
            return Mathf.Lerp(_minimumPos, _maximumPos, target / 100);
        }
    }
}