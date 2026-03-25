using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Drive : MonoBehaviour
{
    [Header("Feet Location")]
    [SerializeField] public Transform feetLocation = null;

    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 10f;
    public float rotationSpeed = 100f;
    public float jumpForce = 8f;
    public float gravity = -15f;

    [Header("Noise Settings")]
    public float walkNoiseRadius = 2f;
    public float runNoiseRadius = 5f;
    public float landNoiseRadius = 4f;
    public float noiseCheckInterval = 0.3f;

    [Header("Noise Multiplier")]
    public float noiseTravelMultiplier = 3f; // Sound travels 3x visual range

    [Header("Audio Clips")]
    public AudioClip walkSound;
    public AudioClip runSound;
    public AudioClip jumpSound;
    public AudioClip landSound;
    [Range(0f, 1f)] public float audioVolume = 0.5f;

    [Header("Visual Feedback")]
    public GameObject noiseVisualPrefab;
    public Color walkColor = new Color(0f, 1f, 1f, 0.5f); // Cyan
    public Color runColor = new Color(1f, 0f, 1f, 0.6f);  // Magenta
    public Color landColor = new Color(1f, 1f, 0f, 0.6f); // Yellow

    [Header("Camera Settings")]
    public Camera playerCamera;
    public float zoomSpeed = 5f;
    public float minZoom = 30f;
    public float maxZoom = 90f;
    public float defaultZoom = 60f;
    public bool invertScroll = true;

    private float currentZoom;
    private float targetZoom;
    private float zoomVelocity;
    private Mouse mouse;


    //

    private float verticalVelocity = 0f;
    private bool isGrounded;
    private bool wasGrounded = true;
    private CharacterController controller;
    private float nextNoiseTime;
    private AudioSource audioSource;

    // Public properties
    public float CurrentSpeed { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsMoving { get; private set; }
    public Vector3 Position => transform.position;

    private void OnEnable()
    {
        // Safe version with null check
        if (controller != null)
        {
            controller.Move(Vector3.zero);
        }
        verticalVelocity = 0f;

        // Start coroutine safely
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(SnapToGround());
        }
    }

    void Start()
    {
        // Add CharacterController
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0, 1, 0);

            
      
            
       

        }

        // Add AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Configure AudioSource
        audioSource.spatialBlend = 0f; // 2D sound
        audioSource.volume = audioVolume;
        audioSource.playOnAwake = false;

        // Check audio clips
        if (walkSound == null) Debug.LogWarning("Walk sound not assigned!");
        if (runSound == null) Debug.LogWarning("Run sound not assigned!");
        if (jumpSound == null) Debug.LogWarning("Jump sound not assigned!");
        if (landSound == null) Debug.LogWarning("Land sound not assigned!");

        
        // Get the main camera if not assigned
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerCamera != null)
        {
            currentZoom = playerCamera.fieldOfView;
            targetZoom = currentZoom;
            defaultZoom = currentZoom;
            Debug.Log($"Camera initialized. FOV: {currentZoom}");
        }
    }


    IEnumerator SnapToGround()
    {
        yield return null; // Wait one frame

        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 100f))
        {
            transform.position = new Vector3(transform.position.x, hit.point.y + 0.1f, transform.position.z);
            Debug.Log($"Player placed on ground at Y={hit.point.y}");
        }

        // Reset velocity
        Drive drive = GetComponent<Drive>();
        if (drive != null)
        {
            drive.verticalVelocity = 0f;
        }
    }


    public void ResetMovement()
    {
        // Reset velocity
        verticalVelocity = 0f;

        // Reset grounded state
        isGrounded = true;
        wasGrounded = true;

        // Reset noise timer
        nextNoiseTime = 0f;

        // Reset CharacterController velocity
        if (controller != null)
        {
            controller.Move(Vector3.zero);
        }

        // Reset movement flags
        IsMoving = false;
        IsRunning = false;
        CurrentSpeed = 0f;

        //Debug.Log("Drive movement reset");
    }

    void Update()
    {
        HandleMovement();
        HandleNoise();
        HandleCameraZoom();
      
    }

    private void HandleMovement()
    {
        // Get input
        float vertical = 0f;
        float horizontal = 0f;
        bool sprintHeld = false;
        bool jumpPressed = false;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) vertical += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) vertical -= 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) horizontal -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizontal += 1f;

        sprintHeld = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        jumpPressed = kb.spaceKey.wasPressedThisFrame;

        // Ground check
        isGrounded = controller.isGrounded;

        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }

        // Calculate speed
        float currentSpeed = (sprintHeld && vertical > 0) ? runSpeed : walkSpeed;

        // Store state - ONLY VERTICAL movement counts for noise!
        IsRunning = sprintHeld && vertical > 0;
        IsMoving = Mathf.Abs(vertical) > 0.1f; // Only forward/backward movement
        CurrentSpeed = IsMoving ? currentSpeed : 0f;

        // Apply rotation (this doesn't make noise)
        transform.Rotate(0, horizontal * rotationSpeed * Time.deltaTime, 0);

        // Apply movement
        Vector3 move = transform.forward * vertical * currentSpeed;
        controller.Move(move * Time.deltaTime);

        // Handle jumping
        if (jumpPressed && isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
            PlaySound(jumpSound, "Jump");
        }

        // Apply gravity
        verticalVelocity += gravity * Time.deltaTime;
        controller.Move(new Vector3(0, verticalVelocity * Time.deltaTime, 0));

        // Check for landing
        if (!wasGrounded && isGrounded)
        {
            OnLand();
        }
        wasGrounded = isGrounded;
    }

    private void HandleNoise()
    {
        if (Time.time < nextNoiseTime) return;
        nextNoiseTime = Time.time + noiseCheckInterval;

        // Only make noise if actually MOVING (vertical input), not just rotating
        if (IsMoving && isGrounded)
        {
            float radius = IsRunning ? runNoiseRadius : walkNoiseRadius;
            Color color = IsRunning ? runColor : walkColor;
            string type = IsRunning ? "RUNNING" : "walking";

            PlaySound(IsRunning ? runSound : walkSound, type);
            MakeNoise(radius, color, type);
        }
    }

    private void OnLand()
    {
        //Debug.Log("Player landed - THUD!");
        PlaySound(landSound, "LANDING");
        MakeNoise(landNoiseRadius, landColor, "LANDING");
    }

    private void MakeNoise(float visualRadius, Color color, string noiseType)
    {
        float soundTravelRadius = visualRadius * noiseTravelMultiplier;

        //Debug.Log($"========== MAKING NOISE: {noiseType} ==========");
        //Debug.Log($"Player position: {transform.position}");
        //Debug.Log($"Visual radius: {visualRadius}, Sound radius: {soundTravelRadius}");

        // 1. Spawn visual effect at FEET height
        if (noiseVisualPrefab != null)
        {
            Vector3 spawnPos;
            float playerY = transform.position.y;

            //Debug.Log($"--- Calculating feet position ---");
            //Debug.Log($"Player Y: {playerY}");

            if (feetLocation != null)
            {
                spawnPos = new Vector3(transform.position.x, feetLocation.position.y, transform.position.z);

            }
            else
            {
                Debug.LogWarning("Feet Location is NULL! Using fallback calculation");

                // Fallback: Assume CharacterController height
                float feetHeight = playerY - 1f; // Standard 2-unit tall player
     
                spawnPos = new Vector3(transform.position.x, feetHeight, transform.position.z);
                //Debug.Log($"Final fallback spawn Y: {spawnPos.y}");
            }


            // Instantiate the visual
            GameObject visual = Instantiate(
                noiseVisualPrefab, 
                new Vector3(spawnPos.x, spawnPos.y -1f, spawnPos.z), 
                Quaternion.identity);

            NoiseVisual noiseVisual = visual.GetComponent<NoiseVisual>();
            

            if (noiseVisual != null)
            {
                noiseVisual.SetNoiseProperties(soundTravelRadius, color);
                //Debug.Log($"NoiseVisual properties set: radius={soundTravelRadius}, color={color}");
            }
            else
            {
                Debug.LogError("NoiseVisual component not found on prefab!");
            }

            visual.transform.up = Vector3.up;
            //Debug.Log($"Visual rotation set to up");
        }
        else
        {
            Debug.LogError("noiseVisualPrefab is not assigned!");
        }

        // 2. Find ALL guards
        //Debug.Log($"--- Notifying guards ---");
        Guard[] allGuards = FindObjectsByType<Guard>(FindObjectsSortMode.None);
        //Debug.Log($"Found {allGuards.Length} guards in scene");

        // 3. Tell each guard about the noise
        foreach (Guard guard in allGuards)
        {

            guard.currentBrain.HearNoise(transform.position, soundTravelRadius * guard.guardHearingSensitivity);

            // Draw debug line to show which guards were notified
            Debug.DrawLine(transform.position, guard.transform.position, Color.cyan, 1.0f);
        }

        //Debug.Log($"========== NOISE COMPLETE ==========");
    }

    private void PlaySound(AudioClip clip, string soundName)
    {
        if (clip == null)
        {
            return;
        }

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip, audioVolume);
        }
    }

    private void HandleCameraZoom()
    {
        if (playerCamera == null || mouse == null) return;

        // Read scroll value from new input system (Y axis of scroll wheel)
        float scrollValue = mouse.scroll.y.ReadValue();

        // Only process if scroll is significant
        if (Mathf.Abs(scrollValue) > 0.01f)
        {
            // Invert if desired (scroll up = zoom in = decrease FOV)
            float direction = invertScroll ? -scrollValue : scrollValue;

            // Update target zoom
            targetZoom += direction * zoomSpeed * 10f;

            // Clamp to limits
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);

            Debug.Log($"Scroll: {scrollValue}, Zoom: {targetZoom:F1}");
        }

        // Smoothly interpolate to target zoom
        currentZoom = Mathf.SmoothDamp(currentZoom, targetZoom, ref zoomVelocity, 0.1f);
        playerCamera.fieldOfView = currentZoom;

        // Reset zoom with middle mouse button
        if (mouse.middleButton.wasPressedThisFrame)
        {
            targetZoom = defaultZoom;
            Debug.Log("Zoom reset to default");
        }
    }


    // Visual debugging
    void OnDrawGizmos()
    {
        if (controller == null) return;

        // Draw controller capsule
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Vector3 bottom = transform.position + Vector3.up * controller.radius;
        Vector3 top = transform.position + Vector3.up * (controller.height - controller.radius);
        Gizmos.DrawWireSphere(bottom, controller.radius);
        Gizmos.DrawWireSphere(top, controller.radius);

        // Draw noise radii
        if (!Application.isPlaying) return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, walkNoiseRadius);

        Gizmos.color = new Color(1f, 0f, 1f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, runNoiseRadius);

        Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, landNoiseRadius);
    }
}