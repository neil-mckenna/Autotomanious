using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class Drive : MonoBehaviour
{
    [Header("References")]
    public Transform cameraPivot;
    public Camera playerCamera;

    private CharacterController controller;

    [Header("Movement")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float acceleration = 10f;
    public float airControl = 0.5f;

    [Header("Jump & Gravity")]
    public float jumpForce = 1.6f;
    public float gravity = -20f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 2f;
    public float verticalClamp = 80f;

    [Header("Mouse Smoothing - NEW")]
    public bool useSmoothing = true;
    public float smoothingAmount = 8f; // Higher = faster response, Lower = smoother
    public float rotationDamping = 10f; // Smooth rotation for camera

    private float yVelocity;
    private float xRotation;
    private float targetXRotation;

    private Vector2 targetMouseDelta;
    private Vector2 currentMouseDelta;

    private Vector3 currentVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        AssignCameraToCanvases();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Initialize rotation values
        targetXRotation = cameraPivot.localEulerAngles.x;
        xRotation = targetXRotation;
    }

    void AssignCameraToCanvases()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvas.worldCamera = playerCamera;
                Debug.Log($"Assigned camera to canvas: {canvas.name}");
            }
        }
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
    }

    // =========================
    //  SMOOTHER MOUSE LOOK
    // =========================
    void HandleMouseLook()
    {
        Vector2 rawMouse = Mouse.current.delta.ReadValue();

        // Apply sensitivity
        Vector2 mouseInput = rawMouse * mouseSensitivity;

        // Simple Lerp smoothing
        currentMouseDelta = Vector2.Lerp(currentMouseDelta, mouseInput, smoothingAmount * Time.deltaTime);

        // Rotate player
        transform.Rotate(Vector3.up * currentMouseDelta.x);

        // Rotate camera with damping
        targetXRotation -= currentMouseDelta.y;
        targetXRotation = Mathf.Clamp(targetXRotation, -verticalClamp, verticalClamp);

        // Smooth camera rotation
        xRotation = Mathf.Lerp(xRotation, targetXRotation, rotationDamping * Time.deltaTime);
        cameraPivot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    // =========================
    // MOVEMENT (unchanged)
    // =========================
    void HandleMovement()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        Vector2 input = Vector2.zero;

        if (kb.wKey.isPressed) input.y += 1;
        if (kb.sKey.isPressed) input.y -= 1;
        if (kb.aKey.isPressed) input.x -= 1;
        if (kb.dKey.isPressed) input.x += 1;

        input = input.normalized;

        bool sprint = kb.leftShiftKey.isPressed;
        float targetSpeed = sprint ? runSpeed : walkSpeed;

        Vector3 move = transform.right * input.x + transform.forward * input.y;

        float control = controller.isGrounded ? 1f : airControl;

        currentVelocity = Vector3.Lerp(
            currentVelocity,
            move * targetSpeed,
            acceleration * control * Time.deltaTime
        );

        // Ground check
        if (controller.isGrounded && yVelocity < 0)
            yVelocity = -2f;

        // Jump
        if (kb.spaceKey.wasPressedThisFrame && controller.isGrounded)
        {
            yVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
        }

        // Gravity
        yVelocity += gravity * Time.deltaTime;

        Vector3 finalMove = currentVelocity + Vector3.up * yVelocity;
        controller.Move(finalMove * Time.deltaTime);
    }

    // =========================
    // RESET
    // =========================
    public void ResetMovement()
    {
        yVelocity = 0f;
        currentVelocity = Vector3.zero;
        currentMouseDelta = Vector2.zero;
        targetMouseDelta = Vector2.zero;

        if (controller != null)
            controller.Move(Vector3.zero);

        xRotation = 0f;
        targetXRotation = 0f;

        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.identity;
    }
}