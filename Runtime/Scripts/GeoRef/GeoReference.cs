using UnityEngine;


namespace GeoRef
{
    public class GeoReference: MonoBehaviour
    {
        public double Lat, Lon;

        GlobalReferencePoint globalRef;

        void Awake()
        {
            globalRef = FindFirstObjectByType<GlobalReferencePoint>();
            if(globalRef == null)
            {
                Debug.LogWarning("No GlobalReferencePoint found in the scene!");
                return;
            }
            var (x,z) = globalRef.GetUnityXZFromLatLon(Lat, Lon);
            var e = 0.001f;
            var xdif = Mathf.Abs(x-transform.position.x);
            var zdif = Mathf.Abs(z-transform.position.z);
            if(xdif > e ||zdif > e)
            {
                Debug.LogWarning($"Position of GeoReference:{transform.parent.name}/{transform.name} does not match the lat/lon values! xdif:{xdif} zdif:{zdif}");
            }
        }

        public void Place()
        {
            globalRef = FindFirstObjectByType<GlobalReferencePoint>();
            if(globalRef == null)
            {
                Debug.LogWarning("No GlobalReferencePoint found in the scene!");
                return;
            }
            var (x,z) = globalRef.GetUnityXZFromLatLon(Lat, Lon);
            transform.position = new Vector3(x, 0, z);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.white;
            float size = 400;
            var top = new Vector3(transform.position.x, size, transform.position.z);
            var bottom = new Vector3(transform.position.x, -size, transform.position.z);
            Gizmos.DrawCube(top, Vector3.one * 10);
            Gizmos.DrawCube(bottom, Vector3.one * 10);
            Gizmos.DrawLine(top, bottom);
        }


    }
}
