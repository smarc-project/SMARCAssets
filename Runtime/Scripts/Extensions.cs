using System;
using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace
{
    public static class Extensions
    {
        
        public static List<T> FindAllChildrenOfType<T>(this Transform item)
        {
            var findResults = new List<T>();

            foreach (Transform child in item)
            {
                if (child.TryGetComponent(out T searchResult))
                {
                    findResults.Add(searchResult);
                }

                findResults.AddRange(child.FindAllChildrenOfType<T>());
            }

            return findResults;
        }

        public static string GetPath(this Transform current, Transform terminalParent = null)
        {
            if (current.parent == null || current.parent == terminalParent)
                return current.name;
            return current.parent.GetPath(terminalParent) + "/" + current.name;
        }

        public static Transform CreatePath(this Transform baseObject, String path)
        {
            var transform = baseObject.Find(path);
            if (transform != null)
            {
                return transform;
            }

            var elements = path.Split("/");
            var lastElement = elements[elements.Length - 1];
            Array.Resize(ref elements, elements.Length - 1);
            var rejoined = string.Join("/", elements);
            
            transform = CreatePath(baseObject, rejoined);
            
            var newObject = new GameObject();
            newObject.name = lastElement;
            newObject.transform.parent = transform;
            newObject.transform.localPosition = Vector3.zero;
            newObject.transform.localRotation = Quaternion.Euler(Vector3.zero);
            
            return newObject.transform;
        }

        public static void ResetArticulationBody(this ArticulationBody body)
        {
            switch (body.dofCount)
            {
                case 1:
                    body.jointPosition = new ArticulationReducedSpace(0f);
                    body.jointForce = new ArticulationReducedSpace(0f);
                    body.jointVelocity = new ArticulationReducedSpace(0f);
                    break;
                case 2:
                    body.jointPosition = new ArticulationReducedSpace(0f, 0f);
                    body.jointForce = new ArticulationReducedSpace(0f, 0f);
                    body.jointVelocity = new ArticulationReducedSpace(0f, 0f);
                    break;
                case 3:
                    body.jointPosition = new ArticulationReducedSpace(0f, 0f, 0f);
                    body.jointForce = new ArticulationReducedSpace(0f, 0f, 0f);
                    body.jointVelocity = new ArticulationReducedSpace(0f, 0f, 0f);
                    break;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}