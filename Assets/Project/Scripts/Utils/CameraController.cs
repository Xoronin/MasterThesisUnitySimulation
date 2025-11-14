using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace RFSimulation.Utils
{

    public class CameraController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 30f;
        public float fastMoveSpeed = 50f;
        public float mouseSensitivity = 2f;

        [Header("Controls")]
        public KeyCode fastMoveKey = KeyCode.LeftShift;

        [Header("Fixed View")]
        public KeyCode fixedViewKey = KeyCode.C;
        public Vector3 fixedViewPosition = new Vector3(65f, 310f, -230f); 
        public Vector3 fixedViewEuler = new Vector3(65f, -90f, 0f);       


        private float rotationX = 0f;
        private float rotationY = 0f;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
        }

        void Update()
        {
            if (UIInput.IsTyping()) return;
            JumpToFixedView();
            HandleMovement();
            HandleMouseLook();
        }

        void HandleMovement()
        {
            float horizontal = Input.GetAxis("Horizontal"); // A/D keys
            float vertical = Input.GetAxis("Vertical");     // W/S keys
            float upDown = 0f;

            if (Input.GetKey(KeyCode.Q)) upDown = -1f; // Down
            if (Input.GetKey(KeyCode.E)) upDown = 1f;  // Up

            Vector3 direction = transform.right * horizontal + transform.forward * vertical + Vector3.up * upDown;

            float currentSpeed = Input.GetKey(fastMoveKey) ? fastMoveSpeed : moveSpeed;

            transform.position += direction * currentSpeed * Time.deltaTime;
        }

        void HandleMouseLook()
        {
            if (Input.GetMouseButton(1))
            {
                Cursor.lockState = CursorLockMode.Locked;

                float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

                rotationX -= mouseY;
                rotationX = Mathf.Clamp(rotationX, -90f, 90f); 
                rotationY += mouseX;

                transform.rotation = Quaternion.Euler(rotationX, rotationY, 0f);
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
            }
        }

        void JumpToFixedView()
        {
            if (Input.GetKeyDown(fixedViewKey))
            {
                transform.position = fixedViewPosition;
                transform.rotation = Quaternion.Euler(fixedViewEuler);

                rotationX = fixedViewEuler.x;
                rotationY = fixedViewEuler.y;
            }
        }
    }
}