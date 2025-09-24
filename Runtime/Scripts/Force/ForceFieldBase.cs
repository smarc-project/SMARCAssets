using MathNet.Numerics.Providers.LinearAlgebra;
using UnityEngine;
using Normal = DefaultNamespace.NormalDistribution;

namespace Force
{
    public enum RandomizationMode
    {
        None,
        Uniform,
        Gaussian,
    }

    [RequireComponent(typeof(Collider))]
    public class ForceFieldBase : MonoBehaviour
    {
        [Header("Force Field Base")]
        [Tooltip("If enabled, only ForcePoints that are UNDER water will be affected")]
        public bool onlyUnderwater = false;
        [Tooltip("If enabled, only ForcePoints that are ABOVE water will be affected")]
        public bool onlyAboveWater = false;

        [Header("Randomization")]
        public RandomizationMode randomizationMode = RandomizationMode.None;
        public float RandomRangeX = 0.0f;
        public float RandomRangeY = 0.0f;
        public float RandomRangeZ = 0.0f;
        Normal normalX;
        Normal normalY;
        Normal normalZ;

        public bool IncludeInVisualizer = true;

        Collider col;

        protected virtual Vector3 Field(Vector3 position)
        {
            Debug.Log($"{this} does not implement Field(Vector3 position)!");
            return Vector3.zero;
        }
        void Awake()
        {
            col = GetComponent<Collider>();
            if (randomizationMode == RandomizationMode.Gaussian)
            {
                normalX = new Normal(0, RandomRangeX);
                normalY = new Normal(0, RandomRangeY);
                normalZ = new Normal(0, RandomRangeZ);
            }
        }

        bool IsInside(Vector3 point)
        {
            var closest = col.ClosestPoint(point);
            return closest == point;
        }

        public Vector3 GetRandomPointInside(bool strictlyInside = false)
        {
            if (col == null) return Vector3.zero;

            //TODO this can be improved for arbitrary collider shapes
            var min = col.bounds.min;
            var max = col.bounds.max;

            Vector3 randomPoint = new Vector3(
                Random.Range(min.x, max.x),
                Random.Range(min.y, max.y),
                Random.Range(min.z, max.z)
            );

            // Ensure the random point is inside the collider
            while (strictlyInside && !IsInside(randomPoint))
            {
                randomPoint = new Vector3(
                    Random.Range(min.x, max.x),
                    Random.Range(min.y, max.y),
                    Random.Range(min.z, max.z)
                );
            }

            return randomPoint;
        }

        public Vector3 GetForceAt(Vector3 position)
        {
            if (!IsInside(position)) return Vector3.zero;
            Vector3 force = Field(position);
            if (randomizationMode == RandomizationMode.None) return force;

            var noise = Vector3.zero;
            switch (randomizationMode)
            {
                case RandomizationMode.Uniform:
                    noise = new Vector3(
                        Random.Range(-RandomRangeX, RandomRangeX),
                        Random.Range(-RandomRangeY, RandomRangeY),
                        Random.Range(-RandomRangeZ, RandomRangeZ)
                    );
                    break;
                case RandomizationMode.Gaussian:
                    noise = new Vector3(
                        (float)normalX.Sample(),
                        (float)normalY.Sample(),
                        (float)normalZ.Sample()
                    );
                    break;
            }
            return force + noise;
        }

        void OnTriggerStay(Collider objCol)
        {
            if (objCol.gameObject.TryGetComponent<ForcePoint>(out ForcePoint fp))
            {
                fp.ApplyForce(GetForceAt(objCol.transform.position), onlyUnderwater, onlyAboveWater);
            }
        }

    }
}
