using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person character controller with mouse look, movement, jumping, and noise-based detection system.
/// This script handles all player movement, camera control, and emits noise for AI detection.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Drive : MonoBehaviour
{
    // ========================================================================
    // PUBLIC REFERENCES
    // ========================================================================

    [Header("=== CAMERA REFERENCES ===")]
    [Tooltip("Transform that the camera is attached to for vertical rotation")]
    public Transform cameraPivot;

    [Tooltip("Main camera for first-person view")]
    public Camera playerCamera;

    [Header("=== AUDIO REFERENCES ===")]
    [Tooltip("Audio source for playing footstep and jump sounds")]
    public AudioSource audioSource;

    [Header("=== AUDIO CLIPS ===")]
    [Tooltip("Random footstep sounds for walking/running")]
    public AudioClip[] footstepClips;

    [Tooltip("Sound played when jumping")]
    public AudioClip jumpClip;

    [Tooltip("Sound played when landing from a fall")]
    public AudioClip landClip;

    [Header("=== AUDIO VOLUME ===")]
    [Range(0f, 1f)]
    [Tooltip("Base volume for footstep sounds")]
    public float footstepVolume = 0.5f;

    [Range(0f, 1f)]
    [Tooltip("Base volume for jump sound")]
    public float jumpVolume = 0.7f;

    [Range(0f, 1f)]
    [Tooltip("Base volume for landing sound")]
    public float landVolume = 0.6f;

    [Header("=== MOVEMENT SETTINGS ===")]
    [Tooltip("Movement speed while walking (not sprinting)")]
    public float walkSpeed = 4f;

    [Tooltip("Movement speed while sprinting")]
    public float runSpeed = 7f;

    [Tooltip("How quickly the player accelerates to target speed")]
    public float acceleration = 10f;

    [Tooltip("Movement control while in air (0 = no control, 1 = full control)")]
    public float airControl = 0.5f;

    [Header("=== JUMP & GRAVITY ===")]
    [Tooltip("Initial upward velocity when jumping")]
    public float jumpForce = 1.6f;

    [Tooltip("Gravity applied each second (negative = downward)")]
    public float gravity = -20f;

    [Header("=== MOUSE LOOK ===")]
    [Tooltip("Mouse sensitivity multiplier")]
    public float mouseSensitivity = 2f;

    [Tooltip("Maximum vertical look angle in degrees (prevents over-rotation)")]
    public float verticalClamp = 80f;

    [Header("=== MOUSE SMOOTHING ===")]
    [Tooltip("Enable mouse movement smoothing for less jittery camera")]
    public bool useSmoothing = true;

    [Tooltip("How quickly mouse movement is smoothed (higher = faster response)")]
    public float smoothingAmount = 8f;

    [Tooltip("How quickly the camera rotates to target rotation")]
    public float rotationDamping = 10f;

    [Header("=== NOISE EMISSION SETTINGS ===")]
    [Tooltip("Radius of noise emitted while walking (detected by AI hearing)")]
    public float walkingNoiseRadius = 10f;

    [Tooltip("Radius of noise emitted while running (detected by AI hearing)")]
    public float runningNoiseRadius = 35f;

    [Tooltip("Minimum time between footstep noises (prevents spam)")]
    public float noiseEmitInterval = 0.5f;

    [Tooltip("Radius of noise emitted when jumping")]
    public float jumpNoiseRadius = 8f;

    [Tooltip("Radius of noise emitted when landing from a fall")]
    public float landNoiseRadius = 15f;

    // ========================================================================
    // PRIVATE REFERENCES
    // ========================================================================

    private CharacterController controller;  // Reference to character controller component
    private Player player;                   // Reference to player component for noise emission

    // Foot location for noise spawning (automatically created at character's feet)
    [SerializeField] private Transform footLocation;
    public Transform FootLocation => footLocation;

    // ========================================================================
    // MOUSE LOOK VARIABLES
    // ========================================================================

    private float xRotation;           // Current camera X rotation
    private float targetXRotation;     // Target camera X rotation (for smoothing)
    private Vector2 targetMouseDelta;  // Target mouse movement this frame
    private Vector2 currentMouseDelta; // Smoothed mouse movement

    // ========================================================================
    // MOVEMENT VARIABLES
    // ========================================================================

    private Vector3 currentVelocity;   // Current horizontal movement velocity
    private float yVelocity;           // Current vertical velocity (for jumping/gravity)

    // ========================================================================
    // NOISE TRACKING
    // ========================================================================

    private float lastNoiseTime;       // Last time a noise was emitted
    private bool wasGrounded = true;   // Whether player was grounded last frame (for landing detection)
    private bool wasMoving = false;    // Whether player was moving last frame

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    void Awake()
    {
        // Get required component references
        controller = GetComponent<CharacterController>();
        player = GetComponent<Player>();

        // Set up camera if not manually assigned
        if (playerCamera == null)
            playerCamera = Camera.main;

        // Assign camera to all UI canvases (so UI renders correctly in world space)
        AssignCameraToCanvases();

        // Lock cursor to center of screen and hide it
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Initialize mouse look angles
        targetXRotation = cameraPivot.localEulerAngles.x;
        xRotation = targetXRotation;
        wasGrounded = controller.isGrounded;

        // Create foot location transform for noise emission
        SetupFootLocation();
    }

    void Update()
    {
        // Handle each system in order
        HandleMouseLook();      // First, rotate camera based on mouse input
        HandleMovement();       // Then, move the character
        HandleNoiseEmission();  // Finally, emit footstep noises for AI detection
    }

    // ========================================================================
    // INITIALIZATION METHODS
    // ========================================================================

    /// <summary>
    /// Creates a foot location transform at the bottom of the character controller.
    /// This is used as the origin point for noise emission.
    /// </summary>
    void SetupFootLocation()
    {
        // Create child GameObject for foot position
        GameObject footObj = new GameObject("FootLocation");
        footObj.transform.SetParent(transform);
        footLocation = footObj.transform;

        // Position at the bottom of the character controller
        if (controller != null)
        {
            // Character controller height is typically 2 units, center is at 0
            // Bottom is at -height/2, add small offset to prevent ground clipping
            float bottomY = -controller.height / 2f;
            footLocation.localPosition = new Vector3(0, bottomY + 0.1f, 0);
            Debug.Log($"FootLocation created at local Y = {footLocation.localPosition.y}");
        }
        else
        {
            // Fallback position if controller not found
            footLocation.localPosition = new Vector3(0, -0.9f, 0);
        }
    }

    /// <summary>
    /// Assigns the player camera to all canvases that use ScreenSpaceCamera render mode.
    /// Ensures UI elements render correctly in world space.
    /// </summary>
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

    // ========================================================================
    // MOUSE LOOK SYSTEM
    // ========================================================================

    /// <summary>
    /// Handles mouse input for camera rotation.
    /// Uses optional smoothing for more natural mouse movement.
    /// </summary>
    void HandleMouseLook()
    {
        // Get raw mouse movement from Input System
        Vector2 rawMouse = Mouse.current.delta.ReadValue();
        Vector2 mouseInput = rawMouse * mouseSensitivity;

        // Apply smoothing if enabled
        if (useSmoothing)
        {
            currentMouseDelta = Vector2.Lerp(currentMouseDelta, mouseInput, smoothingAmount * Time.deltaTime);
        }
        else
        {
            currentMouseDelta = mouseInput;
        }

        // Horizontal rotation (turns the whole character)
        transform.Rotate(Vector3.up * currentMouseDelta.x);

        // Vertical rotation (only rotates the camera pivot)
        targetXRotation -= currentMouseDelta.y;
        targetXRotation = Mathf.Clamp(targetXRotation, -verticalClamp, verticalClamp);

        // Smoothly rotate camera to target rotation
        xRotation = Mathf.Lerp(xRotation, targetXRotation, rotationDamping * Time.deltaTime);
        cameraPivot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    // ========================================================================
    // MOVEMENT SYSTEM
    // ========================================================================

    /// <summary>
    /// Handles WASD movement, jumping, and gravity.
    /// Supports both grounded and airborne movement with different control levels.
    /// </summary>
    void HandleMovement()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        // Get movement input (WASD or arrow keys)
        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed) input.y += 1;
        if (kb.sKey.isPressed) input.y -= 1;
        if (kb.aKey.isPressed) input.x -= 1;
        if (kb.dKey.isPressed) input.x += 1;
        input = input.normalized;  // Prevent faster diagonal movement

        // Determine current speed (walk or run)
        bool sprint = kb.leftShiftKey.isPressed;
        float targetSpeed = sprint ? runSpeed : walkSpeed;

        // Calculate movement direction relative to player orientation
        Vector3 move = transform.right * input.x + transform.forward * input.y;

        // Air control reduces movement influence while airborne
        float control = controller.isGrounded ? 1f : airControl;

        // Smoothly accelerate to target speed
        currentVelocity = Vector3.Lerp(
            currentVelocity,
            move * targetSpeed,
            acceleration * control * Time.deltaTime
        );

        // ===== LANDING DETECTION =====
        bool isGrounded = controller.isGrounded;
        if (!wasGrounded && isGrounded && yVelocity < -5f)
        {
            // Emit landing noise if falling faster than 5 units/second
            EmitNoise(landNoiseRadius, "landing");
        }
        wasGrounded = isGrounded;

        // Reset yVelocity when on ground (prevents accumulating negative velocity)
        if (controller.isGrounded && yVelocity < 0)
            yVelocity = -2f;

        // ===== JUMP =====
        if (kb.spaceKey.wasPressedThisFrame && controller.isGrounded)
        {
            // Calculate jump velocity using physics formula: v = sqrt(2 * g * h)
            yVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
            EmitNoise(jumpNoiseRadius, "jump");
        }

        // Apply gravity
        yVelocity += gravity * Time.deltaTime;

        // Combine horizontal and vertical movement
        Vector3 finalMove = currentVelocity + Vector3.up * yVelocity;
        controller.Move(finalMove * Time.deltaTime);
    }

    // ========================================================================
    // NOISE EMISSION SYSTEM
    // ========================================================================

    /// <summary>
    /// Emits footstep noises at regular intervals while moving.
    /// Noise radius and volume vary based on movement speed (walk vs run).
    /// </summary>
    void HandleNoiseEmission()
    {
        bool isMoving = IsMoving();
        bool isGrounded = controller.isGrounded;
        bool isRunning = IsRunning();

        // Emit noise at regular intervals while moving on ground
        if (isMoving && isGrounded && Time.time - lastNoiseTime > noiseEmitInterval)
        {
            lastNoiseTime = Time.time;
            float noiseRadius = isRunning ? runningNoiseRadius : walkingNoiseRadius;
            string noiseType = isRunning ? "running" : "walking";
            EmitNoise(noiseRadius, noiseType);
        }

        wasMoving = isMoving;
    }

    /// <summary>
    /// Checks if the player is currently moving using WASD or arrow keys.
    /// </summary>
    bool IsMoving()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return false;

        return Mathf.Abs(kb.wKey.ReadValue()) > 0.1f ||
               Mathf.Abs(kb.sKey.ReadValue()) > 0.1f ||
               Mathf.Abs(kb.aKey.ReadValue()) > 0.1f ||
               Mathf.Abs(kb.dKey.ReadValue()) > 0.1f;
    }

    /// <summary>
    /// Checks if the player is sprinting (holding Shift while moving).
    /// </summary>
    bool IsRunning()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return false;
        return kb.leftShiftKey.isPressed && IsMoving();
    }

    /// <summary>
    /// Emits a noise at the player's feet for AI hearing detection.
    /// Calculates volume and radius based on movement type and speed.
    /// </summary>
    /// <param name="radius">Base radius of the noise</param>
    /// <param name="noiseType">Type of noise (walking, running, jump, landing)</param>
    void EmitNoise(float radius, string noiseType)
    {
        if (player == null) return;

        float actualRadius = radius;
        float volume = 1f;       // Detection volume for AI (0-1)
        float audioVolume = 1f;  // Audio playback volume (0-1)
        float currentSpeed = currentVelocity.magnitude;

        // Calculate volume and radius based on noise type
        switch (noiseType)
        {
            case "walking":
                // Walking: quieter, smaller detection radius
                audioVolume = Mathf.Lerp(0.2f, 0.5f, currentSpeed / walkSpeed);
                volume = 0.4f;
                actualRadius = walkingNoiseRadius * (0.5f + audioVolume);
                //Debug.Log($"[FOOTSTEP] Walking - Speed: {currentSpeed:F1}, Audio Volume: {audioVolume:F2}, Detection Volume: {volume:F2}");
                break;

            case "running":
                // Running: louder, larger detection radius
                float speedRatio = (currentSpeed - walkSpeed) / (runSpeed - walkSpeed);
                audioVolume = Mathf.Lerp(0.6f, 1.0f, speedRatio);
                volume = 1.0f;
                actualRadius = runningNoiseRadius * (0.8f + audioVolume * 0.5f);
                //Debug.Log($"[FOOTSTEP] Running - Speed: {currentSpeed:F1}, Audio Volume: {audioVolume:F2}, Detection Volume: {volume:F2}");
                break;

            case "jump":
                // Jump: moderate volume, full detection
                audioVolume = 0.8f;
                volume = 1.0f;
                //Debug.Log($"[NOISE] Jump - Audio Volume: {audioVolume:F2}");
                break;

            case "landing":
                // Landing: volume scales with fall speed
                audioVolume = Mathf.Clamp01(Mathf.Abs(yVelocity) / 15f);
                volume = audioVolume;
                //Debug.Log($"[NOISE] Landing - Fall Speed: {yVelocity:F1}, Audio Volume: {audioVolume:F2}");
                break;
        }

        // Play sound effect
        PlaySoundForNoiseType(noiseType, audioVolume);

        // Get world position at player's feet
        Vector3 noisePosition = GetNoisePosition();

        // Notify the Player component to emit noise for AI detection
        player.MakeNoise(noisePosition, actualRadius, noiseType, volume);

        // Visualize noise radius for debugging
        DebugDrawNoiseSphere(noisePosition, actualRadius, noiseType, volume);
    }

    /// <summary>
    /// Gets the world position where noise should originate (at player's feet).
    /// Uses footLocation if available, otherwise raycasts to find ground.
    /// </summary>
    private Vector3 GetNoisePosition()
    {
        // Use footLocation if available
        if (footLocation != null)
        {
            return footLocation.position;
        }

        // Fallback: raycast to find ground
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down, out hit, 3f))
        {
            return hit.point + Vector3.up * 0.1f;
        }

        // Final fallback at character's base
        return new Vector3(transform.position.x, 0.1f, transform.position.z);
    }

    /// <summary>
    /// Plays the appropriate sound effect based on noise type.
    /// Volume is modulated by movement speed and noise type.
    /// </summary>
    void PlaySoundForNoiseType(string noiseType, float audioVolume)
    {
        if (audioSource == null) return;
        audioVolume = Mathf.Clamp01(audioVolume);

        switch (noiseType)
        {
            case "walking":
                if (footstepClips != null && footstepClips.Length > 0)
                {
                    AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
                    audioSource.PlayOneShot(clip, footstepVolume * audioVolume);
                    //Debug.Log($"[AUDIO] Playing footstep at {audioVolume * footstepVolume:F2} volume");
                }
                break;

            case "running":
                if (footstepClips != null && footstepClips.Length > 0)
                {
                    AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
                    audioSource.PlayOneShot(clip, footstepVolume * audioVolume * 1.2f);  // 20% louder for running
                    //Debug.Log($"[AUDIO] Playing running footstep at {audioVolume * footstepVolume * 1.2f:F2} volume");
                }
                break;

            case "jump":
                if (jumpClip != null)
                {
                    audioSource.PlayOneShot(jumpClip, jumpVolume * audioVolume);
                    //Debug.Log($"[AUDIO] Playing jump at {audioVolume * jumpVolume:F2} volume");
                }
                break;

            case "landing":
                if (landClip != null)
                {
                    audioSource.PlayOneShot(landClip, landVolume * audioVolume);
                    //Debug.Log($"[AUDIO] Playing landing at {audioVolume * landVolume:F2} volume");
                }
                break;
        }
    }

    // ========================================================================
    // DEBUG VISUALIZATION
    // ========================================================================

    /// <summary>
    /// Draws debug visualizations of noise radius for development purposes.
    /// Color intensity scales with volume (quieter = more transparent).
    /// </summary>
    void DebugDrawNoiseSphere(Vector3 position, float radius, string noiseType, float volume = 1f)
    {
        Color color = Color.white;

        // Set color based on noise type, alpha based on volume
        switch (noiseType)
        {
            case "walking": color = new Color(0, 1, 0, volume); break;      // Green
            case "running": color = new Color(1, 1, 0, volume); break;      // Yellow
            case "jump": color = new Color(0, 1, 1, volume); break;      // Cyan
            case "landing": color = new Color(1, 0, 1, volume); break;      // Magenta
            default: color = new Color(1, 1, 1, volume); break;      // White
        }

        // Draw vertical rays to show sphere bounds
        Debug.DrawRay(position + Vector3.up * 0.5f, Vector3.up * radius, color, 1f);
        Debug.DrawRay(position + Vector3.up * 0.5f, Vector3.down * radius, color, 1f);

        // Draw horizontal circle rays to visualize the noise radius
        int segments = 12;
        float angleStep = 360f / segments;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Debug.DrawRay(position + Vector3.up * 0.5f, dir * radius, color, 1f);
        }
    }

    // ========================================================================
    // PUBLIC METHODS
    // ========================================================================

    /// <summary>
    /// Resets all movement and camera state.
    /// Called when respawning the player to ensure clean state.
    /// </summary>
    public void ResetMovement()
    {
        // Reset velocity
        yVelocity = 0f;
        currentVelocity = Vector3.zero;

        // Reset mouse look
        currentMouseDelta = Vector2.zero;
        targetMouseDelta = Vector2.zero;

        // Stop any ongoing movement
        if (controller != null)
            controller.Move(Vector3.zero);

        // Reset camera rotation
        xRotation = 0f;
        targetXRotation = 0f;
        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.identity;

        // Reset noise timing
        lastNoiseTime = 0f;
        wasMoving = false;
    }
}