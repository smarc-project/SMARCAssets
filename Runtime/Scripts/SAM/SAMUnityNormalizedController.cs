using UnityEngine;
using VehicleComponents.Actuators;

namespace Force
{
    public class SAMUnityNormalizedController : MonoBehaviour
    {
        public Hinge yawControl;
        public Hinge pitchControl;
        public VBS vbsControl;
        public Prismatic batteryControl;
        public Propeller propeller1Control;
        public Propeller propeller2Control;

        public float VBSFeedback()
        {
            return vbsControl.GetCurrentValue() / 100f;
        }
        
        public float LCGFeedback()
        {
            return batteryControl.GetCurrentValue() / 100f;
        }
        public void SetRpm1(double rpm)
        {
            propeller1Control.SetRpm((float)rpm * propeller1Control.RPMMax);
        }

        public void SetRpm2(double rpm)
        {
            propeller2Control.SetRpm((float)rpm * propeller2Control.RPMMax);
        }

        public void SetRpm(double rpm1, double rpm2)
        {
            SetRpm1(rpm1);
            SetRpm2(rpm2);
        }

        public void SetRudderAngle(float dr)
        {
            yawControl.SetAngle(dr * yawControl.AngleMax);
        }

        public void SetElevatorAngle(float de)
        {
            pitchControl.SetAngle(de * pitchControl.AngleMax);
        }

        public void SetBatteryPack(float lcg)
        {
            batteryControl.SetPercentage(lcg * 100f);
        }

        public void SetWaterPump(float vbs)
        {
            vbsControl.SetPercentage(vbs * 100);
        }

        public float d_rudder
        {
            get => yawControl.angle;
        }

        public float d_aileron
        {
            get => pitchControl.angle;
        }

        public double rpm1
        {
            get => propeller1Control.rpm;
        }

        public double rpm2
        {
            get => propeller2Control.rpm;
        }

        public double lcg
        {
            get => batteryControl.percentage;
        }

        public double vbs
        {
            get => vbsControl.percentage;
        }
    }
}