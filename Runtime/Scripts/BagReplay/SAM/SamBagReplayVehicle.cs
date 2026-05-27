using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;
using VehicleComponents.Actuators;

namespace Scripts.BagReplay.SAM
{
    public enum SamBagReplayVehicleMode
    {
        TopicControl,
        GhostPosition,
        GhostVelocity,
        GhostAcceleration
    }

    public abstract class SamBagReplayVehicle : MonoBehaviour
    {
        protected static readonly SamBagReplayData EmptySamData = new SamBagReplayData();
        public SamBagReplayVehicleMode replayMode;


        public ArticulationChainComponent chain;
        public BagReplay replay;
        public SamBagReplayAdapter samReplayAdapter;

        public Hinge yaw;
        public Hinge pitch;
        public Propeller frontProp;
        public Propeller backProp;
        public VBS vbs;
        public Prismatic lcg;

        private bool doReset = true;

        protected SamBagReplayData CurrentSamData => samReplayAdapter != null ? samReplayAdapter.CurrentSamData : EmptySamData;
        protected SamBagReplayData NextSamData => samReplayAdapter != null ? samReplayAdapter.NextSamData : EmptySamData;

        protected virtual void Awake()
        {
            replay.OnReplayRestart += RestartListener;
            replay.OnReplayTick += TickReplayMode;
        }

        protected virtual void OnDestroy()
        {
            replay.OnReplayRestart -= RestartListener;
            replay.OnReplayTick -= TickReplayMode;
        }

        protected virtual void Start()
        {
            EnsureReplayAdapter();
            Restart();
        }

        void TickReplayMode()
        {
            if (ApplyPendingReset())
            {
                return;
            }

            ReleaseRootAfterReset();
            ApplyReplayMode(replayMode);
        }

        protected virtual void Restart()
        {
            doReset = true;
        }

        protected void EnsureReplayAdapter()
        {
            if (samReplayAdapter != null)
            {
                if (replay == null)
                {
                    replay = samReplayAdapter.bagReplay;
                }

                samReplayAdapter.ApplyRequiredBindingsToReplay(restartPlayback: true);
                return;
            }

            if (replay == null)
            {
                Debug.LogWarning("SamBagReplayVehicle requires either a BagReplay or SamBagReplayAdapter reference.", this);
                return;
            }

            samReplayAdapter = replay.GetComponent<SamBagReplayAdapter>();
            if (samReplayAdapter == null)
            {
                samReplayAdapter = replay.gameObject.AddComponent<SamBagReplayAdapter>();
            }

            samReplayAdapter.bagReplay = replay;
            samReplayAdapter.ApplyRequiredBindingsToReplay(restartPlayback: true);
        }

        protected bool ApplyPendingReset()
        {
            if (!doReset)
            {
                return false;
            }

            doReset = false;
            if (chain == null)
            {
                return true;
            }

            chain.Restart(
                FRD.ConvertToRUF(NextSamData.PositionMocapFRD),
                FRD.ConvertToRUF(NextSamData.OrientationMocapFRD));
            chain.GetRoot().immovable = true;

            if (vbs != null && vbs.isActiveAndEnabled)
            {
                vbs.GetComponentInParent<ArticulationBody>().jointPosition =
                    new ArticulationReducedSpace(vbs.ComputeTargetValue(CurrentSamData.VbsFeedback));
            }

            if (lcg != null && lcg.isActiveAndEnabled)
            {
                lcg.GetComponentInParent<ArticulationBody>().jointPosition =
                    new ArticulationReducedSpace(lcg.ComputeTargetValue(CurrentSamData.LcgFeedback));
            }

            return true;
        }

        protected void ReleaseRootAfterReset()
        {
            if (chain == null || !chain.GetRoot().immovable)
            {
                return;
            }

            chain.GetRoot().immovable = false;
            chain.GetRoot().linearVelocity = FRD.ConvertToRUF(CurrentSamData.LinearVelocityMocapFRD);
            chain.GetRoot().angularVelocity = FRD.ConvertAngularVelocityToRUF(CurrentSamData.AngularVelocityMocapFRD);
        }

        protected void ApplyReplayMode(SamBagReplayVehicleMode selectedMode)
        {
            switch (selectedMode)
            {
                case SamBagReplayVehicleMode.GhostPosition:
                    DoPositionTeleportUpdate();
                    break;
                case SamBagReplayVehicleMode.GhostVelocity:
                    DoVelocityUpdate();
                    break;
                case SamBagReplayVehicleMode.GhostAcceleration:
                    DoAccelerationUpdate();
                    break;
                case SamBagReplayVehicleMode.TopicControl:
                    SetActuation();
                    break;
            }
        }

        protected virtual void SetActuation()
        {
            yaw?.SetAngle(CurrentSamData.ThrusterHorizontalRad);
            pitch?.SetAngle(CurrentSamData.ThrusterVerticalRad);

            vbs?.SetPercentage(CurrentSamData.VbsCommand);
            lcg?.SetPercentage(CurrentSamData.LcgCommand);

            frontProp?.SetRpm(CurrentSamData.Thruster1Rpm);
            backProp?.SetRpm(CurrentSamData.Thruster2Rpm);
        }


        protected virtual void DoAccelerationUpdate()
        {
            if (chain == null)
            {
                return;
            }

            var root = chain.GetRoot();
            var newVel = FRD.ConvertToRUF(CurrentSamData.LinearVelocityBodyFRD);
            var newAngVel = FRD.ConvertAngularVelocityToRUF(CurrentSamData.AngularVelocityBodyFRD);

            var linearAcc = (newVel - root.transform.InverseTransformVector(root.linearVelocity)) / Time.fixedDeltaTime;
            root.AddRelativeForce(linearAcc, ForceMode.Acceleration);

            var angularAcc = (newAngVel - root.transform.InverseTransformVector(root.angularVelocity)) / Time.fixedDeltaTime;
            root.AddRelativeTorque(angularAcc, ForceMode.Acceleration);
        }

        protected virtual void DoVelocityUpdate()
        {
            if (chain == null)
            {
                return;
            }

            chain.GetRoot().linearVelocity = FRD.ConvertToRUF(CurrentSamData.LinearVelocityMocapFRD);
            chain.GetRoot().angularVelocity = FRD.ConvertAngularVelocityToRUF(CurrentSamData.AngularVelocityMocapFRD);
        }

        protected virtual void DoPositionTeleportUpdate()
        {
            if (chain == null || CurrentSamData.PositionMocapFRD == Vector3.zero)
            {
                return;
            }

            chain.GetRoot().TeleportRoot(
                FRD.ConvertToRUF(CurrentSamData.PositionMocapFRD),
                FRD.ConvertToRUF(CurrentSamData.OrientationMocapFRD));
        }

        private void RestartListener()
        {
            Restart();
        }
    }
}