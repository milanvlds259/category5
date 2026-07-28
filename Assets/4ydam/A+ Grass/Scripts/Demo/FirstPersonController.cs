using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float lookSensitivity = 2f;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private bool lockCursorOnStart = true;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float groundedVerticalVelocity = -2f;

    private float cameraPitch = 0f;
    private Vector3 velocity;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private CharacterController characterController;

    private void Start()
    {
        characterController = GetComponent<CharacterController>();

        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        ReadInput();
        HandleMouseLook();
        HandleMovementAndGravity();
    }

    private void ReadInput()
    {
        Vector2 keyboardMove = Vector2.zero;
        if (Keyboard.current != null)
        {
            keyboardMove.x = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
            keyboardMove.y = (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f);
        }

        Vector2 gamepadMove = Vector2.zero;
        Vector2 gamepadLook = Vector2.zero;
        if (Gamepad.current != null)
        {
            gamepadMove = Gamepad.current.leftStick.ReadValue();
            gamepadLook = Gamepad.current.rightStick.ReadValue();
        }

        moveInput = Vector2.ClampMagnitude(keyboardMove + gamepadMove, 1f);

        Vector2 mouseLook = Vector2.zero;
        if (Mouse.current != null)
        {
            mouseLook = Mouse.current.delta.ReadValue();
        }

        lookInput = mouseLook * 0.02f + gamepadLook * Time.deltaTime * 120f;
    }

    private void HandleMovementAndGravity()
    {
        float moveX = moveInput.x;
        float moveZ = moveInput.y;

        Vector3 moveDirection = (transform.right * moveX + transform.forward * moveZ);
        if (moveDirection.sqrMagnitude > 1f)
        {
            moveDirection.Normalize();
        }

        if (characterController.isGrounded && velocity.y < 0f)
        {
            velocity.y = groundedVerticalVelocity;
        }

        velocity.y += gravity * Time.deltaTime;

        Vector3 frameMove = moveDirection * moveSpeed + Vector3.up * velocity.y;
        characterController.Move(frameMove * Time.deltaTime);
    }

    private void HandleMouseLook()
    {
        float mouseX = lookInput.x * lookSensitivity;
        float mouseY = lookInput.y * lookSensitivity;

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -90f, 90f);

        playerCamera.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

}
