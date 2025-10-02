using UnityEngine;
using NormalDistribution  = DefaultNamespace.NormalDistribution;


namespace VehicleComponents.Sensors
{
    public class RangeReciever : Sensor
    {
        [Header("Range-Reciever")]
        public float distance;
        public GameObject senderObject;
        public float range = 10f;

        //Noise params and generator
        public float noiseMean = 0f;
        public float noiseSigma = 0.1f;
        private NormalDistribution noiseGenerator;


        void Start()
        {
            if(senderObject == null)
            {
                Debug.LogWarning("No sender object set for RangeReciever sensor. Disabling sensor.");
                enabled = false;
            }
            
            distance = float.PositiveInfinity;
            noiseGenerator = new NormalDistribution(noiseMean, noiseSigma);

        }

        public override bool UpdateSensor(double deltaTime)
        {
            if(!enabled)
            {
                return false;
            }

            Vector3 rayOrigin = senderObject.transform.position;
            Vector3 rayEnd = transform.position;
            
        
            if (senderObject != null && Vector3.Distance(rayOrigin,rayEnd) < range)
            {   
                float noise = (float)noiseGenerator.Sample();
                
                distance = Vector3.Distance(rayOrigin,rayEnd)*(1 + noise);
                
            }
            else
            {
                distance = float.PositiveInfinity;
            }

            return true;
        }

    }
}

