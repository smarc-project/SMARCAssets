using System;
using System.Collections.Generic;
using Force;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Linq;

namespace DefaultNamespace
{
    // From: https://gamedev.stackexchange.com/questions/166468/how-do-i-generate-a-sphere-mesh-in-unity
    public static class IcoSphere
    {
        private struct TriangleIndices
        {
            public int v1;
            public int v2;
            public int v3;

            public TriangleIndices(int v1, int v2, int v3)
            {
                this.v1 = v1;
                this.v2 = v2;
                this.v3 = v3;
            }
        }

        // return index of point in the middle of p1 and p2
        private static int getMiddlePoint(int p1, int p2, ref List<Vector3> vertices, ref Dictionary<long, int> cache, float radius)
        {
            // first check if we have it already
            bool firstIsSmaller = p1 < p2;
            long smallerIndex = firstIsSmaller ? p1 : p2;
            long greaterIndex = firstIsSmaller ? p2 : p1;
            long key = (smallerIndex << 32) + greaterIndex;

            int ret;
            if (cache.TryGetValue(key, out ret))
            {
                return ret;
            }

            // not in cache, calculate it
            Vector3 point1 = vertices[p1];
            Vector3 point2 = vertices[p2];
            Vector3 middle = new Vector3
            (
                (point1.x + point2.x) / 2f,
                (point1.y + point2.y) / 2f,
                (point1.z + point2.z) / 2f
            );

            // add vertex makes sure point is on unit sphere
            int i = vertices.Count;
            vertices.Add(middle.normalized * radius);

            // store it, return index
            cache.Add(key, i);

            return i;
        }

        public static void Create(GameObject gameObject, int recursionLevel = 3)
        {
            MeshFilter filter = gameObject.GetComponent<MeshFilter>();
            Mesh mesh = filter.mesh;
            mesh.Clear();
            Vector3[] vertices = gameObject.GetComponent<MeshFilter>().mesh.vertices;
            List<Vector3> vertList = new List<Vector3>();
            Dictionary<long, int> middlePointIndexCache = new Dictionary<long, int>();
            // int index = 0;

            // int recursionLevel = 3;
            float radius = 1f;

            // create 12 vertices of a icosahedron
            float t = (1f + Mathf.Sqrt(5f)) / 2f;

            vertList.Add(new Vector3(-1f, t, 0f).normalized * radius);
            vertList.Add(new Vector3(1f, t, 0f).normalized * radius);
            vertList.Add(new Vector3(-1f, -t, 0f).normalized * radius);
            vertList.Add(new Vector3(1f, -t, 0f).normalized * radius);

            vertList.Add(new Vector3(0f, -1f, t).normalized * radius);
            vertList.Add(new Vector3(0f, 1f, t).normalized * radius);
            vertList.Add(new Vector3(0f, -1f, -t).normalized * radius);
            vertList.Add(new Vector3(0f, 1f, -t).normalized * radius);

            vertList.Add(new Vector3(t, 0f, -1f).normalized * radius);
            vertList.Add(new Vector3(t, 0f, 1f).normalized * radius);
            vertList.Add(new Vector3(-t, 0f, -1f).normalized * radius);
            vertList.Add(new Vector3(-t, 0f, 1f).normalized * radius);


            // create 20 triangles of the icosahedron
            List<TriangleIndices> faces = new List<TriangleIndices>();

            // 5 faces around point 0
            faces.Add(new TriangleIndices(0, 11, 5));
            faces.Add(new TriangleIndices(0, 5, 1));
            faces.Add(new TriangleIndices(0, 1, 7));
            faces.Add(new TriangleIndices(0, 7, 10));
            faces.Add(new TriangleIndices(0, 10, 11));

            // 5 adjacent faces 
            faces.Add(new TriangleIndices(1, 5, 9));
            faces.Add(new TriangleIndices(5, 11, 4));
            faces.Add(new TriangleIndices(11, 10, 2));
            faces.Add(new TriangleIndices(10, 7, 6));
            faces.Add(new TriangleIndices(7, 1, 8));

            // 5 faces around point 3
            faces.Add(new TriangleIndices(3, 9, 4));
            faces.Add(new TriangleIndices(3, 4, 2));
            faces.Add(new TriangleIndices(3, 2, 6));
            faces.Add(new TriangleIndices(3, 6, 8));
            faces.Add(new TriangleIndices(3, 8, 9));

            // 5 adjacent faces 
            faces.Add(new TriangleIndices(4, 9, 5));
            faces.Add(new TriangleIndices(2, 4, 11));
            faces.Add(new TriangleIndices(6, 2, 10));
            faces.Add(new TriangleIndices(8, 6, 7));
            faces.Add(new TriangleIndices(9, 8, 1));


            // refine triangles
            for (int i = 0; i < recursionLevel; i++)
            {
                List<TriangleIndices> faces2 = new List<TriangleIndices>();
                foreach (var tri in faces)
                {
                    // replace triangle by 4 triangles
                    int a = getMiddlePoint(tri.v1, tri.v2, ref vertList, ref middlePointIndexCache, radius);
                    int b = getMiddlePoint(tri.v2, tri.v3, ref vertList, ref middlePointIndexCache, radius);
                    int c = getMiddlePoint(tri.v3, tri.v1, ref vertList, ref middlePointIndexCache, radius);

                    faces2.Add(new TriangleIndices(tri.v1, a, c));
                    faces2.Add(new TriangleIndices(tri.v2, b, a));
                    faces2.Add(new TriangleIndices(tri.v3, c, b));
                    faces2.Add(new TriangleIndices(a, b, c));
                }
                faces = faces2;
            }

            mesh.vertices = vertList.ToArray();

            List<int> triList = new List<int>();
            for (int i = 0; i < faces.Count; i++)
            {
                triList.Add(faces[i].v1);
                triList.Add(faces[i].v2);
                triList.Add(faces[i].v3);
            }
            mesh.triangles = triList.ToArray();
            mesh.uv = new Vector2[vertices.Length];

            Vector3[] normales = new Vector3[vertList.Count];
            for (int i = 0; i < normales.Length; i++)
                normales[i] = vertList[i].normalized;


            mesh.normals = normales;

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.RecalculateNormals();
            //mesh.Optimize();
        }
    }

               
    // From https://forum.unity.com/threads/random-number-with-normal-distribution-passing-average-value.1229193/
    /// <summary>
    /// A gaussian or normal distribution.
    /// </summary>
    public class NormalDistribution
    {

        private double m_factor;

        public NormalDistribution(double mean, double sigma)
        {
            Mean = mean;
            Sigma = sigma;
            Variance = sigma * sigma;
        }

        public double Mean { get; private set; }

        public double Variance { get; private set; }

        public double Sigma { get; private set; }

        private bool m_useLast;

        private double m_y2;

        /// <summary>
        /// Sample a value from distribution for a given random varible.
        /// </summary>
        /// <returns>A value from the distribution</returns>
        public double Sample()
        {
            double x1, x2, w, y1;

            if (m_useLast)
            {
                y1 = m_y2;
                m_useLast = false;
            }
            else
            {
                do
                {
                    x1 = 2.0 * Random.value - 1.0;
                    x2 = 2.0 * Random.value - 1.0;
                    w = x1 * x1 + x2 * x2;
                }
                while (w >= 1.0);

                w = Math.Sqrt(-2.0 * Math.Log(w) / w);
                y1 = x1 * w;
                m_y2 = x2 * w;
                m_useLast = true;
            }

            return Mean + y1 * Sigma;
        }

    }


    public static class Utils
    {
        public static DenseMatrix Skew(this MatrixBuilder<Double> mb, Vector<double> cb)
        {
            return DenseMatrix.OfArray(new[,]
            {
                {0, -cb[2], cb[1]},
                {cb[2], 0, -cb[0]},
                {-cb[1], cb[0], 0}
            });
        }

        public static Vector3 ToVector3(this Vector<double> vec)
        {
            return new Vector3((float)vec[0], (float)vec[1], (float)vec[2]);
        }

        public static GameObject FindChildWithTag(GameObject parent, string tag)
        {
            GameObject child = null;
            foreach (Transform transform in parent.transform)
            {
                if (transform.CompareTag(tag))
                {
                    child = transform.gameObject;
                    break;
                }
            }
            return child;
        }

        public static GameObject FindDeepChildWithTag(GameObject parent, string tag)
        {
            if (parent.transform.CompareTag(tag)) return parent;

            foreach (Transform child in parent.transform)
            {
                var result_go = FindDeepChildWithTag(child.gameObject, tag);
                if (result_go != null) return result_go;
            }
            return null;
        }


        public static GameObject FindDeepChildWithName(GameObject parent, string name)
        {
            if (parent.name == name) return parent;

            Transform result = parent.transform.Find(name);
            if (result != null) return result.gameObject;

            foreach (Transform child in parent.transform)
            {
                var result_go = FindDeepChildWithName(child.gameObject, name);
                if (result_go != null) return result_go;
            }
            return null;
        }

        public static Transform[] FindAllChildrenWithName(GameObject parent, string name, bool activeOnly = true)
        {
            var transforms = parent.transform.GetComponentsInChildren<Transform>().Where(t => t.name == name);
            if (activeOnly)
                transforms = transforms.Where(t => t.gameObject.activeInHierarchy);
            return transforms.ToArray();
        }

        public static GameObject FindParentWithTag(GameObject self, string tag, bool returnTopLevel)
        {
            Transform parent_tf = self.transform.parent;
            if (parent_tf == null)
            {
                if (returnTopLevel) return self;
                else return null;
            }
            if (parent_tf.CompareTag(tag))
            {
                return parent_tf.gameObject;
            }
            return FindParentWithTag(parent_tf.gameObject, tag, returnTopLevel);
        }

        public static string GetGameObjectPath(GameObject obj)
        {
            string path = "/" + obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = "/" + obj.name + path;
            }
            return path;
        }

        /// <summary>
        /// Converts a world position to a local position in the given canvas' RectTransform space.
        /// Result is suitable for setting RectTransform.anchoredPosition (when using the same reference rect).
        /// </summary>
        public static Vector2 WorldToCanvasPosition(Canvas targetCanvas, Camera worldCamera, Vector3 worldPosition)
        {
            if (!targetCanvas) return Vector2.zero;

            // 1) World -> Screen pixels (this uses the CURRENT game view & camera pixel rect)
            Vector3 sp = worldCamera.WorldToScreenPoint(worldPosition);

            // Behind camera: choose your behavior (hide/clamp/etc.)
            if (sp.z < 0f)
                return new Vector2(float.NaN, float.NaN);

            // 2) Screen -> Local point in the target canvas' rect space
            // For ScreenSpaceOverlay, pass null. Otherwise pass the canvas' worldCamera.
            Camera uiCam = (targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : targetCanvas.worldCamera;

            RectTransform canvasRect = targetCanvas.transform as RectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                sp,
                uiCam,
                out Vector2 localPoint
            );

            return localPoint;
        }

        /// <summary>
        /// Convenience: set a RectTransform's anchoredPosition from a world point.
        /// Works best when the rect is under the same canvas you pass in.
        /// </summary>
        public static void SetAnchoredFromWorld(RectTransform rect, Canvas canvas, Camera worldCamera, Vector3 worldPosition)
        {
            Vector2 local = WorldToCanvasPosition(canvas, worldCamera, worldPosition);

            // If you returned NaN for behind-camera, you can hide here.
            if (float.IsNaN(local.x))
            {
                rect.gameObject.SetActive(false);
                return;
            }

            rect.gameObject.SetActive(true);
            rect.anchoredPosition = local;
        }

        /// <summary>
        /// Calculates the center of mass of an ArticulationBody and (optionally) its children. This will crawl the
        /// ArticulationBody hierarchy and compute the weighted average of the center of mass
        /// and return the CoM in world coordinates.
        /// </summary>
        public static Vector3 GetCenterOfMass(ArticulationBody ab, bool includeChildren = true)
        {
            MixedBody b = new(ab, null);
            return b.GetTotalConnectedCenterOfMass(includeChildren);
        }

        
    }

}