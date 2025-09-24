using UnityEngine;

public class ReferenceUpdater : MonoBehaviour
{
    public bool Controller_mode = true;
    

    [Header("Movement Settings")]
    public float movementSpeed = 5.0f; // Speed of movement
    public float verticalSpeed = 3.0f; // Speed of moving up and down (space and shift)

    private Vector3 velocity = Vector3.zero;
    
    public void OnTickChange(bool tick)
    {
        Controller_mode = tick;
    }

    void FixedUpdate()
    {
        if (Controller_mode)
        {
            // Handle movement input
            HandleMovementInput();

            // Move the object based on velocity
            transform.position += velocity * Time.fixedDeltaTime;
        }
    }

    void HandleMovementInput()
    {
        // Reset velocity
        velocity = Vector3.zero;

        // WASD controls for movement in the X and Z plane
        if (Input.GetKey(KeyCode.W)) // Forward (increase Z)
            velocity.z = movementSpeed;
        if (Input.GetKey(KeyCode.S)) // Backward (decrease Z)
            velocity.z = -movementSpeed;
        if (Input.GetKey(KeyCode.A)) // Left (decrease X)
            velocity.x = -movementSpeed;
        if (Input.GetKey(KeyCode.D)) // Right (increase X)
            velocity.x = movementSpeed;

        // Space and Shift for moving up and down
        if (Input.GetKey(KeyCode.Space)) // Up
            velocity.y = verticalSpeed;
        if (Input.GetKey(KeyCode.LeftShift)) // Down
            velocity.y = -verticalSpeed;
    }
}
