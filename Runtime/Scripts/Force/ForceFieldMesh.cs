using UnityEngine;

namespace Force
{
    public enum MeshForceFieldMode
    {
        TowardsSkin,
        AwayFromSkin,
    }
    
    public enum MeshForceFieldMagnitudeMode
    {
        Constant,
        BodyVelocity,
    }

    public class ForceFieldMesh : ForceFieldBase
    {

        [Header("Mesh Force Field")]
        [Tooltip("The mesh collider that defines the force field's shape.")]
        public MeshForceFieldMode mode = MeshForceFieldMode.TowardsSkin;
        public MeshForceFieldMagnitudeMode magnitudeMode = MeshForceFieldMagnitudeMode.Constant;

        [Tooltip("The collider that defines the force field's shape.")]
        public Collider InnerCollider;

        [Tooltip("If force magnitude is set to Constant, this will be the force vector's magnitude.")]
        public float ForceMagnitude = 1f;
        [Tooltip("If force magnitude is set to BodyVelocity, Magnitude of the force will be Velocity * VelocityMultiplier.")]
        public float VelocityMultiplier = 1f;


        [Tooltip("If set, the force field will use the Rigidbody's velocity to calculate the force magnitude.")]
        public Rigidbody RB;
        [Tooltip("If set, the force field will use the ArticulationBody's velocity to calculate the force magnitude.")]
        public ArticulationBody AB;


        public Transform VizPoint;
        public float VizSize = 1f;
        public float VizAlpha = 1f;

        MixedBody body;


        void Start()
        {
            body = new MixedBody(AB, RB);
            if (!body.isValid && magnitudeMode == MeshForceFieldMagnitudeMode.BodyVelocity)
            {
                Debug.LogError("ForceFieldMesh requires either a Rigidbody or an ArticulationBody to calculate the force magnitude based on body velocity.");
                enabled = false;
            }
            if (InnerCollider == null)
            {
                Debug.LogWarning("InnerCollider is not set. This is needed to define the force field's shape.");
                enabled = false;
            }
        }

        protected override Vector3 Field(Vector3 position)
        {
            // we find the closes point on the collider, which COULD be a mesh collider
            // for arbitrary shapes.
            // then the point from the position to the closest point is the vecotr of the force.
            var closestPoint = InnerCollider.ClosestPoint(position);
            Vector3 forceVector;
            if (mode == MeshForceFieldMode.TowardsSkin) forceVector = closestPoint - position;
            else forceVector = position - closestPoint; 

            forceVector.Normalize();
            switch (magnitudeMode)
            {
                case MeshForceFieldMagnitudeMode.Constant:
                    forceVector *= ForceMagnitude;
                    break;

                case MeshForceFieldMagnitudeMode.BodyVelocity:
                    var vel = body.velocity;
                    // the magnitude of the force is proportinal to how much the body velocity and
                    // the force vector are aligned.
                    float magnitude = Vector3.Dot(vel, forceVector);
                    forceVector *= magnitude;
                    // if the magnitude is negative, we invert the force vector
                    // if (magnitude < 0f) forceVector *= -1f;
                    forceVector *= VelocityMultiplier;
                    break;
            }
            return forceVector;
        }




        void OnDrawGizmos()
        {
            if (VizPoint == null || InnerCollider == null) return;
            body ??= new MixedBody(AB, RB);
            if (!body.isValid) return;

            var v = Field(VizPoint.position);
            Gizmos.color = new Color(v.x, v.y, v.z, VizAlpha);
            Gizmos.DrawRay(VizPoint.position, v.normalized * VizSize);

            var closestPoint = InnerCollider.ClosestPoint(VizPoint.position);
            Gizmos.color = new Color(1f, 1f, 1f, VizAlpha);
            Gizmos.DrawSphere(closestPoint, VizSize * 0.1f);
        }


    }
}