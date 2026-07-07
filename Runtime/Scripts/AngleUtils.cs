using UnityEngine;

namespace DefaultNamespace
{
    public static class AngleUtils
    {
        public static float NormalizeAngle(float angle)
        {
            angle = (angle + 180f) % 360f;
            if (angle < 0)
                angle += 360f;
            return angle - 180f;
        }

        public static Vector3 NormalizeAngles(Vector3 angles)
        {
            return new Vector3(
                NormalizeAngle(angles.x),
                NormalizeAngle(angles.y),
                NormalizeAngle(angles.z)
            );
        }
    }
}