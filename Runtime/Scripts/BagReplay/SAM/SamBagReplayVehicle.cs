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

    public class SamBagReplayVehicle : MonoBehaviour
    {
        protected static readonly SamBagReplayData EmptySamData = new SamBagReplayData();


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
        protected virtual SamBagReplayVehicleMode ReplayMode => SamBagReplayVehicleMode.TopicControl;

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

        protected virtual void TickReplayMode()
        {
            if (ApplyPendingReset())
            {
                return;
            }

            ReleaseRootAfterReset();
            ApplyReplayMode(ReplayMode);
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

            GetMocapWorldPose(NextSamData, out var position, out var rotation);
            chain.Restart(position, rotation);
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
            chain.GetRoot().linearVelocity = MocapVectorToWorld(CurrentSamData.LinearVelocityMocapFRD);
            chain.GetRoot().angularVelocity = MocapAngularVelocityToWorld(CurrentSamData.AngularVelocityMocapFRD);
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

            chain.GetRoot().linearVelocity = MocapVectorToWorld(CurrentSamData.LinearVelocityMocapFRD);
            chain.GetRoot().angularVelocity = MocapAngularVelocityToWorld(CurrentSamData.AngularVelocityMocapFRD);
        }

        protected virtual void DoPositionTeleportUpdate()
        {
            if (chain == null || CurrentSamData.PositionMocapFRD == Vector3.zero)
            {
                return;
            }

            GetMocapWorldPose(CurrentSamData, out var position, out var rotation);
            chain.GetRoot().TeleportRoot(
                position,
                rotation);
        }

        protected void GetMocapWorldPose(SamBagReplayData samData, out Vector3 position, out Quaternion rotation)
        {
            var localPosition = FRD.ConvertToRUF(samData.PositionMocapFRD);
            var localRotation = FRD.ConvertToRUF(samData.OrientationMocapFRD);
            var parent = GetReplayParent();

            if (parent == null)
            {
                position = localPosition;
                rotation = localRotation;
                return;
            }

            position = parent.TransformPoint(localPosition);
            rotation = parent.rotation * localRotation;
        }

        protected Vector3 MocapVectorToWorld(Vector3 vectorFrd)
        {
            var localVector = FRD.ConvertToRUF(vectorFrd);
            var parent = GetReplayParent();
            return parent == null ? localVector : parent.TransformDirection(localVector);
        }

        protected Vector3 MocapAngularVelocityToWorld(Vector3 angularVelocityFrd)
        {
            var localAngularVelocity = FRD.ConvertAngularVelocityToRUF(angularVelocityFrd);
            var parent = GetReplayParent();
            return parent == null ? localAngularVelocity : parent.TransformDirection(localAngularVelocity);
        }

        private Transform GetReplayParent()
        {
            if (chain == null)
            {
                return null;
            }

            var root = chain.GetRoot();
            return root != null ? root.transform.parent : null;
        }

        private void RestartListener()
        {
            Restart();
        }
    }
    
}
