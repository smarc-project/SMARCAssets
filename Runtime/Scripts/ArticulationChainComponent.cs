using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ArticulationChainComponent : MonoBehaviour
{
    public List<ArticulationBody> bodyParts;
    public List<DriveController> controllers;
    public Dictionary<ArticulationBody, DriveController> DriveControllers;

    private ArticulationBody root;

    public void DoAwake()
    {
        bodyParts = FindArticulationBodies(transform);
        root = bodyParts.Find(body => body.isRoot);

        DriveControllers = bodyParts.Select(bp => (bp, new DriveController(bp))).ToDictionary(tuple => tuple.bp, tuple => tuple.Item2);
        controllers = DriveControllers.Values.ToList();
    }


    public void Restart(Vector3 position, Quaternion rotation)
    {
        if (DriveControllers == null || root == null) DoAwake();

        ZeroForces();
        root.TeleportRoot(position, rotation);
        ZeroForces();
    }

    public void ZeroForces()
    {
        foreach (var bodyPart in DriveControllers.Values)
        {   
            bodyPart.ResetArticulationBody();
        }
    }

    public class DriveController
    {
        public DriveParameters XParameters;
        public DriveParameters YParameters;
        public DriveParameters ZParameters;
        private ArticulationReducedSpace initialPosition;
        public readonly ArticulationBody articulationBody;

        public DriveController(ArticulationBody articulationBody)
        {
            this.articulationBody = articulationBody;
            initialPosition = this.articulationBody.jointPosition;
            XParameters = DriveParameters.CreateParameters(articulationBody.xDrive);
            YParameters = DriveParameters.CreateParameters(articulationBody.yDrive);
            ZParameters = DriveParameters.CreateParameters(articulationBody.zDrive);
            this.articulationBody.GetJointForces(new List<float>());
        }

        public void SetDriveTargets(float x, float y, float z)
        {
            articulationBody.SetDriveTarget(ArticulationDriveAxis.X, x);
            articulationBody.SetDriveTarget(ArticulationDriveAxis.Y, y);
            articulationBody.SetDriveTarget(ArticulationDriveAxis.Z, z);
        }

        public void SetDriveTargetsNorm(float x, float y, float z)
        {
            articulationBody.SetDriveTarget(ArticulationDriveAxis.X, ComputeFromNormalizedDriveTarget(XParameters, x));
            articulationBody.SetDriveTarget(ArticulationDriveAxis.Y, ComputeFromNormalizedDriveTarget(YParameters, y));
            articulationBody.SetDriveTarget(ArticulationDriveAxis.Z, ComputeFromNormalizedDriveTarget(ZParameters, z));
        }

        public void ResetArticulationBody()
        {
            switch (articulationBody.dofCount)
            {
                case 1:
                    articulationBody.jointVelocity = new ArticulationReducedSpace(0f);
                    articulationBody.jointForce = new ArticulationReducedSpace(0f);
                    break;
                case 2:
                    articulationBody.jointVelocity = new ArticulationReducedSpace(0f, 0f);
                    articulationBody.jointForce = new ArticulationReducedSpace(0f, 0f);
                    break;
                case 3:
                    articulationBody.jointVelocity = new ArticulationReducedSpace(0f, 0f, 0f);
                    articulationBody.jointForce = new ArticulationReducedSpace(0f, 0f, 0f);
                    break;
            }
            
            articulationBody.jointPosition = initialPosition;
            articulationBody.SetDriveTarget(ArticulationDriveAxis.X, 0);
            articulationBody.linearVelocity = Vector3.zero;
            articulationBody.angularVelocity = Vector3.zero;
        }

        public void SetDriveStrength(float x)
        {
            SetDriveStrengthsNorm(x, x, x);
        }

        public void SetDriveStrengthsNorm(float x, float y, float z)
        {
            articulationBody.SetDriveForceLimit(ArticulationDriveAxis.X, ComputeFromNormalizedDriveStrength(XParameters, x));
            articulationBody.SetDriveForceLimit(ArticulationDriveAxis.Y, ComputeFromNormalizedDriveStrength(YParameters, y));
            articulationBody.SetDriveForceLimit(ArticulationDriveAxis.Z, ComputeFromNormalizedDriveStrength(ZParameters, z));
        }


        public float ComputeNormalizedDriveTarget(DriveParameters drive, float unnormalized)
        {
            return 2 * ((unnormalized - drive.lowerLimit) / (drive.upperLimit - drive.lowerLimit)) - 1;
        }

        public float ComputeFromNormalizedDriveTarget(DriveParameters drive, float normalized)
        {
            return drive.lowerLimit + (normalized + 1) / 2 * (drive.upperLimit - drive.lowerLimit);
        }

        public float ComputeFromNormalizedDriveStrength(DriveParameters drive, float normalized)
        {
            return (normalized + 1f) * 0.5f * drive.forceLimit;
        }
    }

    public struct DriveParameters
    {
        public float upperLimit;
        public float lowerLimit;
        public float stiffness;
        public float damping;
        public float forceLimit;

        public static DriveParameters CreateParameters(ArticulationDrive drive)
        {
            return new DriveParameters
            {
                upperLimit = drive.upperLimit,
                lowerLimit = drive.lowerLimit,
                stiffness = drive.stiffness,
                damping = drive.damping,
                forceLimit = drive.forceLimit,
            };
        }
    }

    public ArticulationBody GetRoot()
    {
        if(root == null) DoAwake();
        return root;
    }

    public List<ArticulationBody> FindArticulationBodies(Transform item)
    {
        var findArticulationBodies = new List<ArticulationBody>();

        var comp = item.GetComponent<ArticulationBody>();
        if (comp != null)
        {
            findArticulationBodies.Add(comp);
        }

        foreach (Transform child in item)
        {
            findArticulationBodies.AddRange(FindArticulationBodies(child));
        }

        return findArticulationBodies;
    }
}