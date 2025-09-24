using System;
using System.Collections.Generic;
using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
// using MinimumSnapTrajectory = Trajectory.MinimumSnapTrajectory;

// Directives for publishing messages
using VehicleComponents.Actuators;
using DefaultNamespace.LookUpTable;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;

namespace DroneController
{
    /// <summary>
    /// Enumeration of possible drone controller states. Enables future expansion of various controllers
    /// </summary>
    public enum DroneControllerState
    {
        TrackingControl = 0,
        LoadControl = 1,
        TrackingControlMinSnap = 2,
        TrackingControlNormalizedXYZ = 3,
        TrackingControlNormalizedXY = 4
    }

    /// <summary>
    /// Convenience class for returning controller error and converting it into ROS supported message formats
    /// </summary>
    public class ControllerError
    {
        private Vector<double> positionError;
        private Vector<double> velocityError;
        private Vector<double> orientationError;

        // Constructors
        public ControllerError(Vector<double> position, Vector<double> velocity, Vector<double> orientationError)
        {
            this.positionError = position;
            this.velocityError = velocity;
            this.orientationError = orientationError;
        }

        public ControllerError()
        {
            this.positionError = DenseVector.OfArray(new double[] { 0, 0, 0 });
            this.velocityError = DenseVector.OfArray(new double[] { 0, 0, 0 });
        }

        /// <summary>
        /// Controller error message format takes the form of the following array structurs
        /// Index: [0,3): Position Error
        /// Index: [3,6): Velocity Error
        /// Index: [6,9): Orientation Error
        /// </summary>
        public double[] ReturnMessageFormat()
        {
            // Create a list to store the combined elements
            // NOTE: going between types a lot here due to need to be an double []
            List<double> result = new List<double>();

            // concatenates list using C# built in AddRange
            result.AddRange(this.positionError);
            result.AddRange(this.velocityError);
            result.AddRange(this.orientationError);

            // Convert the list to an array as this is the required ROS datatype
            double[] arrayResult = result.ToArray();
            return arrayResult;
        }
    }

    /// <summary>
    /// Geometric tracking drone controller for quadrotor drone. Class can implement multiple methods but currently only implements geometric tracking control
    /// </summary>
    public class DroneController : MonoBehaviour
    {
        [Header("Basics")] [Tooltip("Baselink of drone")]
        public GameObject BaseLink;

        ArticulationBody baseLinkDroneAB;

        [Header("Tracking")] [Tooltip("An object to follow for drone tracking control")]
        public Transform TrackingTargetTF;

        [Header("Drone Configuration")]
        [Tooltip(
            "The euclidean distance from the center of gravity of the drone to rotor (assumes square prop configuration)")]
        /// <value> Distance from the center of the quadrotor to each propeller (assumes square prop configuration) (m) </value>
        public double rotorMomentArm = 0.315;

        [Tooltip(
            "The ratio specifying how much of the rotor force is translated into torque around the drones 3rd axis (normal to the rotor plane)")]
        /// <value> The amount of rotor torque that gets translated into the drones 3rd axis. </value>
        public double torqueCoefficient = 0.08;

        [Header("Propellors")] public Transform propFR;
        public Transform propFL, propBR, propBL;

        /// <value> Maps the individual rotor forces to a global force and moment set</value>
        public Matrix<double> propellorForceToGlobalMap;

        public Matrix<double> propellerForceToGlobalMapInverse;

        Propeller[] propellers;

        [Header("Control Mode")] [Tooltip("Currently only implemented controller is TrackingControl")]
        public DroneControllerState controllerState = DroneControllerState.TrackingControlNormalizedXYZ;
        [Tooltip("Distance error cap for tracking control when using normalized tracking. Smaller=less aggressive")]
        public double DistanceErrorCapNormalized = 2;
        [Tooltip("Distance error cap for tracking control when using non-normalized tracking. Smaller=less aggressive")]
        public double DistanceErrorCapFree = 10;



        ////////////////// SYSTEM SPECIFIC //////////////////
        double massQuadrotor;
        Matrix<double> inertiaJ;
        const int NUM_PROPS = 4;

        // Cached values to avoid recompute
        static readonly Vector<double> e3 = DenseVector.OfArray(new double[] { 0, 0, 1 });
        double g;
        float dt;

        // State Tracking
        Matrix<double> desiredAttitude_prev;

        Vector<double> targetAngularVelocity_prev;


        [Header("Velocity control params")]
        public Vector3 TargetVelocity = Vector3.zero;
        Vector3 TargetAccel = Vector3.zero;

        // Initialization function
        void Start()
        {
            propellers = new Propeller[4];
            propellers[0] = propFL.GetComponent<Propeller>();
            propellers[1] = propFR.GetComponent<Propeller>();
            propellers[2] = propBR.GetComponent<Propeller>();
            propellers[3] = propBL.GetComponent<Propeller>();

            propellorForceToGlobalMap = DenseMatrix.OfArray(new double[,]
                {
                    { 1, 1, 1, 1 },
                    { rotorMomentArm, 0, -rotorMomentArm, 0 },
                    { 0, -rotorMomentArm, 0, rotorMomentArm },
                    { torqueCoefficient, -torqueCoefficient, torqueCoefficient, -torqueCoefficient }
                }
            ); // NOTE: Checked

            propellerForceToGlobalMapInverse = propellorForceToGlobalMap.Inverse();

            baseLinkDroneAB = BaseLink.GetComponent<ArticulationBody>();
            massQuadrotor = baseLinkDroneAB.mass; // Quadrotor mass (kg)

            // Creating diagonal matrix of inertia (no off diagonal terms)
            double[] diagonal =
                { baseLinkDroneAB.inertiaTensor.x, baseLinkDroneAB.inertiaTensor.z, baseLinkDroneAB.inertiaTensor.y };
            inertiaJ = DenseMatrix.CreateDiagonal(3, 3, index => diagonal[index]);
            g = Physics.gravity.magnitude;

            // Creating identity matrices (3 x 3) for previous frame transforms
            desiredAttitude_prev = DenseMatrix.CreateDiagonal(3, 3, 1.0);


            // Creating zero vector for previous frame angular velocities
            targetAngularVelocity_prev = DenseVector.OfArray(new double[] { 0, 0, 0 });

            dt = Time.fixedDeltaTime;
        }
        

        /// <summary>
        /// Main control loop of the system running at the unity fixed update time step.
        /// </summary>
        void FixedUpdate()
        {
            double f = 0;
            Vector<double> M = DenseVector.OfArray(new double[] { 0, 0, 0 });
            ControllerError controllerError = new ControllerError();

            if (controllerState is DroneControllerState.TrackingControl
                or DroneControllerState.TrackingControlNormalizedXYZ
                or DroneControllerState.TrackingControlNormalizedXY)
            {
                (f, M, controllerError) = ComputeTrackingControl();
            }
            else if (controllerState == DroneControllerState.LoadControl)
            {
                // TODO: Implement me
                Debug.LogWarning(
                    $"{System.Enum.GetName(typeof(DroneControllerState), DroneControllerState.LoadControl)} state is not implemented");
                Debug.LogWarning("Changing controller to Tracking control");
                controllerState = DroneControllerState.TrackingControl;
                return;
            }
            else if (controllerState == DroneControllerState.TrackingControlMinSnap)
            {
                // TODO: Implement me
                Debug.LogWarning(
                    $"{System.Enum.GetName(typeof(DroneControllerState), DroneControllerState.TrackingControlMinSnap)} state is not implemented");
                Debug.LogWarning("Changing controller to Tracking control");
                controllerState = DroneControllerState.TrackingControl;
                return;
            }
            else
            {
                Debug.Log("Controller state is outside possible states");
                Debug.LogWarning("Changing controller to Tracking control");
                controllerState = DroneControllerState.TrackingControl;
                return;
            }

            float[] currPropellerRPMs = ComputeRPMs(f, M);
            ApplyRPMs(currPropellerRPMs);
        }


        /// <summary>
        /// Tracking controller is implemented per "Geometric tracking control of a quadrotor UAV on SE(3)"
        /// Source: https://ieeexplore.ieee.org/document/5717652
        /// </summary>
        (double, Vector<double>, ControllerError) ComputeTrackingControl()
        {
            double f;
            Vector<double> M;

            ////////////////// SYSTEM SPECIFIC //////////////////
            // Gains
            double kx = 13 * massQuadrotor;
            double kv = 5.6 * massQuadrotor;
            double kR = 8.81;
            double kW = 1.54;
            /////////////////////////////////////////////////////


            // Quadrotor states
            // NOTE: checked these
            Vector<double> dronePosition = BaseLink.transform.position.To<ENU>().ToDense();
            Vector<double> droneVelocity = baseLinkDroneAB.linearVelocity.To<ENU>().ToDense();
            Matrix<double> currentAttitude = DenseMatrix.OfArray(new double[,]
            {
                { BaseLink.transform.right.x, BaseLink.transform.forward.x, BaseLink.transform.up.x },
                { BaseLink.transform.right.z, BaseLink.transform.forward.z, BaseLink.transform.up.z },
                { BaseLink.transform.right.y, BaseLink.transform.forward.y, BaseLink.transform.up.y }
            });
            Vector<double> droneAngularVelocity = -1f * BaseLink.transform
                .InverseTransformDirection(baseLinkDroneAB.angularVelocity).To<ENU>().ToDense();

            // Desired states
            Vector<double> targetPosition;
            Vector<double> targetVelocity;
            Vector<double> targetAccel;

            Vector<double> errorTrackingPosition;

            // Control
            targetPosition = TrackingTargetTF.position.To<ENU>().ToDense();
            targetVelocity = DenseVector.OfArray(new double[] { 0, 0, 0 });
            targetAccel = DenseVector.OfArray(new double[] { 0, 0, 0 });
            errorTrackingPosition = dronePosition - targetPosition;
            
            if (controllerState is DroneControllerState.TrackingControlNormalizedXYZ
                or DroneControllerState.TrackingControlNormalizedXY)
            {
                int endIndex;
                if (controllerState is DroneControllerState.TrackingControlNormalizedXYZ)
                {
                    // Full normalization of entire vector
                    endIndex = errorTrackingPosition.Count;
                }
                else 
                {
                    // If statement above guards for any other cases arriving here
                    // Normalization of X,Y component only
                    endIndex = errorTrackingPosition.Count - 1;
                }

                Vector<double> errorTrackingPositionSubVec =
                    errorTrackingPosition.SubVector(0, endIndex);
                errorTrackingPositionSubVec =
                    Math.Min(DistanceErrorCapNormalized, errorTrackingPositionSubVec.Norm(2)) *
                    errorTrackingPositionSubVec.Normalize(2);
                errorTrackingPosition.SetSubVector(0, endIndex, errorTrackingPositionSubVec);
            }
            else
            {
                // Handles DroneControllerState.TrackingControl
                errorTrackingPosition = (dronePosition - targetPosition) *
                                        Math.Min(DistanceErrorCapFree / (dronePosition - targetPosition).Norm(2),
                                            1);
            }
    

            Vector<double> errorTrackingVelocity = droneVelocity - targetVelocity;

            Vector<double> pidGain = _ComputePIDTerm(
                kx,
                kv,
                g,
                massQuadrotor,
                targetAccel,
                errorTrackingPosition,
                errorTrackingVelocity);


            Matrix<double> desiredAttitude = _ComputeDesiredAttitudeVectors(pidGain);

            Vector<double> targetAngularVelocity = DenseVector.OfArray(new double[] { 0, 0, 0 });
            Vector<double> targetAngularVelocityDot = (targetAngularVelocity - targetAngularVelocity_prev) / dt;

            Vector<double> errorRotation = 0.5 * _VeeMap(desiredAttitude.Transpose() * currentAttitude -
                                                         currentAttitude.Transpose() * desiredAttitude);
            Vector<double> eOmega = droneAngularVelocity -
                                    currentAttitude.Transpose() * desiredAttitude * targetAngularVelocity;
            var controllerError =
                new ControllerError(errorTrackingPosition, errorTrackingVelocity, errorRotation);

            f = pidGain * (currentAttitude * e3);
            M = -kR * errorRotation - kW * eOmega + _Cross(droneAngularVelocity, inertiaJ * droneAngularVelocity) -
                inertiaJ * (_HatMap(droneAngularVelocity) * currentAttitude.Transpose() * desiredAttitude *
                    targetAngularVelocity - currentAttitude.Transpose() * desiredAttitude * targetAngularVelocityDot);

            // Updating trailing values needed at each computation
            desiredAttitude_prev = desiredAttitude;
            targetAngularVelocity_prev = targetAngularVelocity;

            return (f, M, controllerError);
        }




        /// <summary>
        /// Stacks force scaler and moment vecotrs into single Vector
        /// </summary>
        private static Vector<double> _StackForceMomentVector(double f, Vector<double> moments)
        {
            return DenseVector.OfArray(new double[] { f, moments[0], moments[1], moments[2] });
        }

        /// <summary>
        /// Computes RPMs needed for control regardless of controller state
        /// </summary>
        /// <param name="f"> Desired forces </param>
        /// <param name="M"> Desired moments </param>
        float[] ComputeRPMs(double f, Vector<double> M)
        {
            Vector<double> globalForces = _StackForceMomentVector(f, M);
            Vector<double> F_star = propellerForceToGlobalMapInverse * globalForces;
            Vector<double> F = F_star;

            for (int i = 0; i < NUM_PROPS; i++)
            {
                if (F[i] < 0)
                {
                    F[i] = 0;
                }
            }

            // Constructs propeller RPM vector 
            float[] currPropVals = { 0, 0, 0, 0 };
            for (int i = 0; i < propellers.Length; i++)
                currPropVals[i] = (float)F[i] / propellers[i].RPMToForceMultiplier;
            return currPropVals;
        }

        /// <summary>
        /// Applies RPMs to unity drone object 
        ///
        /// Ensures thats propellers does not have a negative RPM
        /// </summary>
        void ApplyRPMs(float[] propellersRPMs)
        {
            // Debug.Log($"RPM: {propellersRPMs[0]:F2},{propellersRPMs[1]:F2},{propellersRPMs[2]:F2},{propellersRPMs[3]:F2}"); // desired position
            for (int i = 0; i < propellers.Length; i++)
            {
                // Now, all props should always have positive rpms, but just in case...
                if (propellersRPMs[i] < 0)
                {
                    Debug.LogWarning("Propeller " + i + " has negative RPMs: " + propellersRPMs[i]);
                    propellersRPMs[i] = 0;
                }

                propellers[i].SetRpm(propellersRPMs[i]);
            }
        }

        /// <summary>
        /// Computes "PID"-like gain for tracking controller
        /// </summary>
        private static Vector<double> _ComputePIDTerm(double gainX,
            double gainV,
            double gravityMagnitude,
            double mass,
            Vector<double> desiredAcceleration,
            Vector<double> errorPosition,
            Vector<double> errorVelocity)
        {
            // This is upper term of equation (12) from geometric tracking paper
            Vector<double> pid = -gainX * errorPosition - gainV * errorVelocity + mass * gravityMagnitude * e3 +
                                 mass * desiredAcceleration;
            return pid;
        }

        /// <summary>
        /// Computes desired attitude vector for quadrotor from Geometric Tracking and Control.
        ///
        /// Computes the desired headings for body vectors 1,2,3 where 3 is normal to rotor plane.
        /// Equations that pertain to this section can be found in Tracking Errors Section
        /// </summary>
        private static Matrix<double> _ComputeDesiredAttitudeVectors(Vector<double> pid)
        {
            Vector<double> b1d = DenseVector.OfArray(new double[] { Math.Sqrt(2) / 2, -Math.Sqrt(2) / 2, 0 });
            Vector<double> b3d = pid / pid.Norm(2);
            Vector<double> b2d = _Cross(b3d, b1d) / _Cross(b3d, b1d).Norm(2);
            b1d = _Cross(b2d, b3d);
            // NOTE: If needed can update for performance to just manually creating matrix like before can easily be re-implemented here
            Matrix<double> R_sb_d = CreateMatrix.DenseOfColumns(new Vector<double>[] { b1d, b2d, b3d });

            return R_sb_d;
        }


        /// <summary>
        /// Cross product operation for R^3 vectors
        /// </summary>
        /// TODO: Does cross product exist in unity math
        private static Vector<double> _Cross(Vector<double> a, Vector<double> b)
        {
            // Calculate each component of the cross product
            double c1 = a[1] * b[2] - a[2] * b[1];
            double c2 = a[2] * b[0] - a[0] * b[2];
            double c3 = a[0] * b[1] - a[1] * b[0];

            // Create a new vector for the result
            return DenseVector.OfArray(new double[] { c1, c2, c3 });
        }

        /// <summary>
        ///  Constructs skew symmetric matrix from vector. Also known as the hat map.
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        private static Matrix<double> _HatMap(Vector<double> v)
        {
            return DenseMatrix.OfArray(new double[,]
            {
                { 0, -v[2], v[1] },
                { v[2], 0, -v[0] },
                { -v[1], v[0], 0 }
            });
        }


        /// <summary>
        ///  Constructs vector from skew symmetric matrix. Also known as the vee map.
        /// </summary>
        /// <param name="S"></param>
        /// <returns></returns>
        private static Vector<double> _VeeMap(Matrix<double> S)
        {
            return DenseVector.OfArray(new double[] { S[2, 1], S[0, 2], S[1, 0] });
        }
    }
}
