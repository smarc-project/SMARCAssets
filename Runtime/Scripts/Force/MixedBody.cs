using System;
using System.Collections.Generic;
using UnityEngine;


namespace Force
{

    // Because Arti bodies and Rigid bodies dont share
    // an ancestor, even though they share like 99% of the
    // methods and semantics...public class MixedBody
    public class MixedBody
    {
        public ArticulationBody ab;
        public Rigidbody rb;

        public bool isValid => ab != null || rb != null;

        ArticulationBody[] childrenABs;

        public MixedBody(ArticulationBody ab, Rigidbody rb)
        {
            this.ab = ab;
            this.rb = rb;
        }

        public GameObject gameObject
        {
            get {return ab ? ab.gameObject : rb.gameObject; }
        }

        public Transform transform
        {
            get {return ab ? ab.transform : rb.transform; }
        }

        public bool automaticCenterOfMass
        {
            get {return ab ? ab.automaticCenterOfMass : rb.automaticCenterOfMass; }
            set {
                if(ab != null) ab.automaticCenterOfMass = value;
                else rb.automaticCenterOfMass = value;
                }
        }

        public Vector3 centerOfMass
        {
            get {return ab ? ab.centerOfMass : rb.centerOfMass; }
            set {
                if(ab != null) ab.centerOfMass = value;
                else rb.centerOfMass = value;
            }
        }

        public bool useGravity
        {
            get {return ab ? ab.useGravity : rb.useGravity; }
            set {
                if(ab != null) ab.useGravity = value;
                else rb.useGravity = value;
            }
        }

        public float mass
        {
            get {return ab ? ab.mass : rb.mass; }
            set {
                if(ab != null) ab.mass = value;
                else rb.mass = value;
            }
        }
        public ArticulationDrive xDrive
        {
            get{if (ab != null){return ab.xDrive;}
                else { throw new InvalidOperationException("The 'ab' part of MixedBody is null. Cannot get 'xDrive'.");}
            }
            set{if (ab != null){ab.xDrive = value;
                } else { throw new InvalidOperationException("The 'ab' part of MixedBody is null. Cannot set 'xDrive'.");}
            }
        }

        public ArticulationReducedSpace jointPosition
        {
            get 
            { 
                if (ab != null) { return ab.jointPosition; } 
                else { throw new InvalidOperationException("The 'ab' part of MixedBody is null. 'jointPosition' is only valid for ArticulationBody."); } 
            }
            set 
            { 
                if (ab != null) { ab.jointPosition = value; } 
                else { throw new InvalidOperationException("The 'ab' part of MixedBody is null. 'jointPosition' is only valid for ArticulationBody."); } 
            }
        }


        public float drag
        {
            get {return ab ? ab.linearDamping : rb.linearDamping; }
            set {
                if(ab != null) ab.linearDamping = value;
                else rb.linearDamping = value;
            }
        }

        public float angularDrag
        {
            get {return ab ? ab.angularDamping : rb.angularDamping; }
            set {
                if(ab != null) ab.angularDamping = value;
                else rb.angularDamping = value;
            }
        }
        public Vector3 angularVelocity
        {
            get {return ab ? ab.angularVelocity : rb.angularVelocity; }
            set {
                if(ab != null) ab.angularVelocity = value;
                else rb.angularVelocity = value;
            }
        } 
        public Vector3 velocity
        {
            get {return ab ? ab.linearVelocity : rb.linearVelocity; }
            set {
                if(ab != null) ab.linearVelocity = value;
                else rb.linearVelocity = value;
            }
        }

        public Vector3 localVelocity
        {
            get {return transform.InverseTransformVector(velocity); }
            set {velocity = transform.TransformVector(value); }
        }
        
        public Vector3 position
        {
            get {return ab ? ab.transform.position : rb.transform.position; }
        }

        public Quaternion rotation
        {
            get {return ab ? ab.transform.rotation : rb.transform.rotation; }
        }
        
        public Vector3 localPosition
        {
            get {return ab ? ab.transform.localPosition : rb.transform.localPosition; }
        }

        public Quaternion localRotation
        {
            get {return ab ? ab.transform.localRotation : rb.transform.localRotation; }
        }


        public void AddForceAtPosition(Vector3 force, Vector3 position, ForceMode mode = ForceMode.Force)
        {
            if(ab != null)
                ab.AddForceAtPosition(force, position, mode);
            else
                rb.AddForceAtPosition(force, position, mode);
        }
        public void AddTorque(Vector3 torque, ForceMode mode = ForceMode.Force)
        {
            if (ab != null)
                ab.AddTorque(torque, mode);
            else 
                rb.AddTorque(torque, mode);
        } 

        public void ConnectToJoint(Joint j)
        {
            if(ab != null) j.connectedArticulationBody = ab;
            else j.connectedBody = rb;
        }

        /// <summary>
        /// Get the total mass of the entire connected articulation body chain.
        /// Crawls the entire articulation tree, so be careful running this in updates...
        /// </summary>
        public float GetTotalConnectedMass(bool includeChildren = true, bool ignoreNonGravityBodies = true)
        {
            // too much hassle to follow through an arbitrary system
            // of rigidbodies and joints...
            if(ab == null) return rb.mass;

            if (childrenABs == null) childrenABs = ab.GetComponentsInChildren<ArticulationBody>();
            float totalMass = ab.mass;
            if (!includeChildren) return totalMass;
            foreach (var body in childrenABs)
            {
                if (body == ab) continue;
                if (!body.isActiveAndEnabled) continue;
                if (ignoreNonGravityBodies && !body.useGravity) continue;
                totalMass += body.mass;
            }
            return totalMass;
        }

        /// <summary>
        /// Get the center of mass of the entire connected articulation body chain in world coords.
        /// Crawls the entire articulation tree, so be careful running this in updates...
        /// </summary>
        public Vector3 GetTotalConnectedCenterOfMass(bool includeChildren = true, bool ignoreNonGravityBodies = true)
        {
            Vector3 com = (position + transform.TransformVector(centerOfMass)) * mass;
            if (!includeChildren) return com/mass;
            if (ab == null) return com/mass;
            var totalMass = mass;
            if (childrenABs == null) childrenABs = ab.GetComponentsInChildren<ArticulationBody>();
            foreach (var body in childrenABs)
            {
                if (body == ab) continue;
                if (!body.isActiveAndEnabled) continue;
                if (ignoreNonGravityBodies && !body.useGravity) continue;
                totalMass += body.mass;
                com += (body.transform.position + body.transform.TransformVector(body.centerOfMass)) * body.mass;
            }

            if (totalMass <= 0f) return Vector3.zero;

            return com / totalMass;
        }
        
    
        public void SetDriveTarget(ArticulationDriveAxis axis, float target)
        {
            if (ab != null)
            {
                ab.SetDriveTarget(axis, target);
            }
            else
            {
                throw new InvalidOperationException("The 'ab' part of MixedBody is null. 'SetDriveTarget' is only valid for ArticulationBody.");
            }
        }    
        public void SetDriveTargetVelocity(ArticulationDriveAxis axis, float velocity)
        {
            if (ab != null)
            {
                ab.SetDriveTargetVelocity(axis, velocity);
            }
            else
            {
                throw new InvalidOperationException("The 'ab' part of MixedBody is null. 'SetDriveTargetVelocity' is only valid for ArticulationBody.");
            }
        }

    }
}
