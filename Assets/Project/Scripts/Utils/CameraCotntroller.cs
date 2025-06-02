using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 10f;
    public float fastMoveSpeed = 20f;
    public float mouseSensitivity = 2f;
    
    [Header("Controls")]
    public KeyCode fastMoveKey = KeyCode.LeftShift;
    
    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        // Lock cursor to center of screen when right-clicking
        Cursor.lockState = CursorLockMode.None;
    }

    void Update()
    {
        HandleMovement();
        HandleMouseLook();
    }

    void HandleMovement()
    {
        // Get input
        float horizontal = Input.GetAxis("Horizontal"); // A/D keys
        float vertical = Input.GetAxis("Vertical");     // W/S keys
        float upDown = 0f;
        
        // Up/Down movement
        if (Input.GetKey(KeyCode.Q)) upDown = -1f; // Down
        if (Input.GetKey(KeyCode.E)) upDown = 1f;  // Up
        
        // Calculate movement direction
        Vector3 direction = transform.right * horizontal + transform.forward * vertical + Vector3.up * upDown;
        
        // Choose speed (fast when holding Shift)
        float currentSpeed = Input.GetKey(fastMoveKey) ? fastMoveSpeed : moveSpeed;
        
        // Move the camera
        transform.position += direction * currentSpeed * Time.deltaTime;
    }

    void HandleMouseLook()
    {
        // Only rotate camera when right mouse button is held
        if (Input.GetMouseButton(1))
        {
            // Lock cursor while looking around
            Cursor.lockState = CursorLockMode.Locked;
            
            // Get mouse input
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            
            // Apply rotation
            rotationX -= mouseY;
            rotationX = Mathf.Clamp(rotationX, -90f, 90f); // Prevent over-rotation
            rotationY += mouseX;
            
            transform.rotation = Quaternion.Euler(rotationX, rotationY, 0f);
        }
        else
        {
            // Unlock cursor when not looking around
            Cursor.lockState = CursorLockMode.None;
        }
    }
}