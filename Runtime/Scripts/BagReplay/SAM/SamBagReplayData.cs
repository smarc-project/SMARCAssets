using UnityEngine;

namespace Scripts.BagReplay.SAM
{
    public class SamBagReplayData
    {
        public float VbsCommand { get; set; }
        public float LcgCommand { get; set; }
        public int Thruster1Rpm { get; set; }
        public int Thruster2Rpm { get; set; }
        public float ThrusterHorizontalRad { get; set; }
        public float ThrusterVerticalRad { get; set; }
        public Vector3 PositionMocapFRD { get; set; }
        public Quaternion OrientationMocapFRD { get; set; }
        public Vector3 LinearVelocityMocapFRD { get; set; }
        public Vector3 AngularVelocityMocapFRD { get; set; }
        public float VbsFeedback { get; set; }
        public float LcgFeedback { get; set; }
        public Vector3 LinearVelocityBodyFRD { get; set; }
        public Vector3 AngularVelocityBodyFRD { get; set; }
    }
}
