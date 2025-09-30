using UnityEngine;

public class Gimbal : MonoBehaviour
{
    void LateUpdate()
    {
        if (transform.parent == null)
            return;

        // 1) Extract parent's yaw (rotation around world up)
        Vector3 parentForward = Vector3.ProjectOnPlane(transform.parent.forward, Vector3.up);
        Quaternion yawOnly = Quaternion.identity;

        if (parentForward.sqrMagnitude > 1e-6f)
        {
            yawOnly = Quaternion.LookRotation(parentForward.normalized, Vector3.up);
        }

        // 2) Define "down" forward and "up" vector based on parent's yaw
        Vector3 forward = Vector3.down;
        Vector3 up = yawOnly * Vector3.forward;

        // 3) Apply rotation
        transform.rotation = Quaternion.LookRotation(forward, up);
    }
}
