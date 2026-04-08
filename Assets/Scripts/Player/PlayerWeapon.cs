using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;

// ============================================================================
// PLAYER WEAPON - THROWABLE GRENADE SYSTEM (SMOKE BOMBS & FLASHBANGS)
// ============================================================================
// 
// This script handles the player's throwable weapons system with features:
// 1. Two weapon types: Smoke Bombs (area denial) and Flashbangs (stun)
// 2. Physics-based throwing with gravity multiplier for arc
// 3. Shoulder-based throwing (right or left hand)
// 4. Collision avoidance (prevents throwing into walls)
// 5. Ammo management with UI display
// 6. Input system integration
// 7. Audio feedback
// 8. Optional throw animation support
//
// THROWING MECHANICS:
// - Grenades are thrown from shoulder position
// - Direction follows camera forward + slight upward arc
// - Temporary collision ignore prevents hitting player
// - Gravity multiplier creates realistic arc trajectory
//
// ============================================================================

public class PlayerWeapon : MonoBehaviour
{
    // ========================================================================
    // SERIALIZED FIELDS - WEAPON SETTINGS
    // ========================================================================

    [Header("=== WEAPON PREFABS ===")]
    [Tooltip("Smoke bomb prefab (creates area denial cloud)")]
    [SerializeField] private GameObject smokeBombPrefab;

    [Tooltip("Flashbang prefab (stuns and blinds enemies)")]
    [SerializeField] private GameObject flashBangPrefab;

    [Header("=== AMMO SETTINGS ===")]
    [Tooltip("Maximum smoke bombs the player can carry")]
    [SerializeField] private int maxSmokeBombs = 6;

    [Tooltip("Maximum flashbangs the player can carry")]
    [SerializeField] private int maxFlashBangs = 6;

    [Header("=== THROWING PHYSICS ===")]
    [Tooltip("Force applied when throwing (meters per second)")]
    [SerializeField] private float throwForce = 8f;

    [Tooltip("Minimum time between throws (seconds)")]
    [SerializeField] private float throwCooldown = 1f;

    [Tooltip("Upward angle force (0 = flat, higher = more arc)")]
    [SerializeField] private float upwardForce = 1.5f;

    [Tooltip("Gravity multiplier for grenade arc (higher = steeper drop)")]
    [SerializeField] private float gravityMultiplier = 2.5f;

    [Header("=== THROW POSITION SETTINGS ===")]
    [Tooltip("Right shoulder transform (where grenade spawns)")]
    [SerializeField] private Transform rightShoulder;

    [Tooltip("Left shoulder transform (where grenade spawns)")]
    [SerializeField] private Transform leftShoulder;

    [Tooltip("True = throw from right shoulder, False = left shoulder")]
    [SerializeField] private bool useRightHand = true;

    [Header("=== COLLISION SETTINGS ===")]
    [Tooltip("Layers that block grenade spawning (walls, etc.)")]
    [SerializeField] private LayerMask throwCollisionMask = -1;

    [Tooltip("Radius for checking spawn obstruction")]
    [SerializeField] private float collisionCheckRadius = 0.2f;

    [Header("=== UI SETTINGS ===")]
    [Tooltip("Name of UI text element for smoke bomb count")]
    [SerializeField] private string smokeUITextName = "SmokeCountText";

    [Tooltip("Name of UI text element for flashbang count")]
    [SerializeField] private string flashUITextName = "FlashCountText";

    [Header("=== AUDIO SETTINGS ===")]
    [Tooltip("Sound played when throwing a grenade")]
    [SerializeField] private AudioClip throwSound;

    [Tooltip("Volume of throw sound (0-1)")]
    [SerializeField] private float throwVolume = 0.7f;

    [Header("=== INPUT SETTINGS ===")]
    [Tooltip("Input Action Asset containing Player action map")]
    [SerializeField] private InputActionAsset inputActionAsset;

    // ========================================================================
    // PRIVATE FIELDS - AMMO TRACKING
    // ========================================================================

    private int currentSmokeBombs;   // Current smoke bomb count
    private int currentFlashBangs;   // Current flashbang count
    private float lastThrowTime;     // Last time a grenade was thrown

    // ========================================================================
    // PRIVATE FIELDS - COMPONENT REFERENCES
    // ========================================================================

    private Camera playerCamera;           // Reference to player's camera
    private TextMeshProUGUI smokeCountText;  // UI text for smoke bombs
    private TextMeshProUGUI flashCountText;  // UI text for flashbangs
    private AudioSource audioSource;        // Audio source for throw sound
    private Collider playerCollider;        // Player's collider (for collision ignore)
    private Animator playerAnimator;        // Optional throw animation

    // ========================================================================
    // PRIVATE FIELDS - INPUT SYSTEM
    // ========================================================================

    private InputActionMap playerActionMap;
    private InputAction smokeBombAction;
    private InputAction flashBangAction;

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    void Start()
    {
        // Initialize ammo (start with 0, can be picked up)
        currentSmokeBombs = 0;
        currentFlashBangs = 0;

        // Get component references
        playerCamera = Camera.main;

        // Setup audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Get player collider for collision ignoring
        playerCollider = GetComponent<Collider>();
        if (playerCollider == null)
            playerCollider = GetComponentInChildren<Collider>();

        // Get animator for throw animation (optional)
        playerAnimator = GetComponent<Animator>();

        // Auto-find shoulder transforms if not assigned
        if (rightShoulder == null || leftShoulder == null)
        {
            FindShoulderTransforms();
        }

        // Setup input and UI
        SetupInput();
        FindUIElements();
        UpdateUI();
    }

    void OnDestroy()
    {
        // Clean up input event subscriptions
        if (smokeBombAction != null)
            smokeBombAction.performed -= OnSmokeBomb;

        if (flashBangAction != null)
            flashBangAction.performed -= OnFlashBang;

        if (playerActionMap != null)
            playerActionMap.Disable();
    }

    // ========================================================================
    // INITIALIZATION METHODS
    // ========================================================================

    /// <summary>
    /// Finds shoulder transforms by searching for bone names.
    /// Falls back to creating empty transforms if no bones found.
    /// </summary>
    private void FindShoulderTransforms()
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>();

        foreach (Transform child in allChildren)
        {
            string lowerName = child.name.ToLower();

            if (lowerName.Contains("shoulder") || lowerName.Contains("clavicle"))
            {
                if (lowerName.Contains("right") || lowerName.Contains("r_"))
                {
                    rightShoulder = child;
                }
                else if (lowerName.Contains("left") || lowerName.Contains("l_"))
                {
                    leftShoulder = child;
                }
            }
        }

        // Create fallback transforms if not found
        if (rightShoulder == null)
        {
            GameObject rightShoulderObj = new GameObject("RightShoulder");
            rightShoulderObj.transform.parent = transform;
            rightShoulderObj.transform.localPosition = new Vector3(0.3f, 1.2f, 0.2f);
            rightShoulder = rightShoulderObj.transform;
        }

        if (leftShoulder == null)
        {
            GameObject leftShoulderObj = new GameObject("LeftShoulder");
            leftShoulderObj.transform.parent = transform;
            leftShoulderObj.transform.localPosition = new Vector3(-0.3f, 1.2f, 0.2f);
            leftShoulder = leftShoulderObj.transform;
        }
    }

    /// <summary>
    /// Gets the current throw spawn point based on throwing hand preference.
    /// </summary>
    private Transform GetCurrentThrowPoint()
    {
        return useRightHand ? rightShoulder : leftShoulder;
    }

    /// <summary>
    /// Sets up input actions from Input Action Asset.
    /// </summary>
    private void SetupInput()
    {
        if (inputActionAsset == null)
        {
            Debug.LogError("Input Action Asset not assigned in PlayerWeapon!");
            return;
        }

        playerActionMap = inputActionAsset.FindActionMap("Player");

        if (playerActionMap == null)
        {
            Debug.LogError("Could not find 'Player' action map in Input Action Asset!");
            return;
        }

        smokeBombAction = playerActionMap.FindAction("SmokeBomb");
        flashBangAction = playerActionMap.FindAction("FlashBang");

        if (smokeBombAction == null)
            Debug.LogError("Could not find 'SmokeBomb' action!");

        if (flashBangAction == null)
            Debug.LogError("Could not find 'FlashBang' action!");

        playerActionMap.Enable();

        if (smokeBombAction != null)
            smokeBombAction.performed += OnSmokeBomb;

        if (flashBangAction != null)
            flashBangAction.performed += OnFlashBang;
    }

    /// <summary>
    /// Finds UI text elements by name in the scene.
    /// </summary>
    private void FindUIElements()
    {
        GameObject smokeTextObj = GameObject.Find(smokeUITextName);
        if (smokeTextObj != null)
        {
            smokeCountText = smokeTextObj.GetComponent<TextMeshProUGUI>();
        }

        GameObject flashTextObj = GameObject.Find(flashUITextName);
        if (flashTextObj != null)
        {
            flashCountText = flashTextObj.GetComponent<TextMeshProUGUI>();
        }
    }

    /// <summary>
    /// Updates UI text elements with current ammo counts.
    /// </summary>
    private void UpdateUI()
    {
        if (smokeCountText != null)
            smokeCountText.text = $"SMOKE: {currentSmokeBombs}";

        if (flashCountText != null)
            flashCountText.text = $"FLASH: {currentFlashBangs}";
    }

    // ========================================================================
    // INPUT HANDLERS
    // ========================================================================

    // Add this method to your PlayerWeapon class
    private bool CanThrow()
    {
        
        // Check cooldown
        if (Time.time < lastThrowTime + throwCooldown)
        {
            float remainingCooldown = throwCooldown - (Time.time - lastThrowTime);
            Debug.Log($"Cannot throw: On cooldown for {remainingCooldown:F1} more seconds");
            return false;
        }

        // Check if player is stunned or dead (optional)
        Player player = GetComponent<Player>();
        if (player == null)
        {
            Debug.Log("Cannot throw: Player is dead or null!");
            return false;
        }

        return true;
    }

    private void OnSmokeBomb(InputAction.CallbackContext context)
    {

        if (context.phase == InputActionPhase.Performed && currentSmokeBombs > 0 && CanThrow())
        {
            ThrowSmokeBomb();
        }
        
    }

    private void OnFlashBang(InputAction.CallbackContext context)
    {

        if (context.phase == InputActionPhase.Performed && currentFlashBangs > 0 && CanThrow())
        {
            ThrowFlashBang();
        }
            
        
    }

    // ========================================================================
    // THROWING METHODS
    // ========================================================================

    /// <summary>
    /// Throws a smoke bomb from the current shoulder position.
    /// </summary>
    private void ThrowSmokeBomb()
    {
        if (!TryGetThrowPosition(out Vector3 throwPosition, out Vector3 throwDirection))
            return;

        currentSmokeBombs--;
        lastThrowTime = Time.time;

        // Trigger throw animation if available
        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("Throw");
        }

        // Instantiate and throw the smoke bomb
        GameObject smoke = Instantiate(smokeBombPrefab, throwPosition, Quaternion.identity);
        ApplyThrowPhysics(smoke, throwDirection);

        PlayThrowSound();
        UpdateUI();
        Debug.Log($"Smoke bomb thrown from {(useRightHand ? "right" : "left")} shoulder! Remaining: {currentSmokeBombs}");
    }

    /// <summary>
    /// Throws a flashbang from the current shoulder position.
    /// </summary>
    private void ThrowFlashBang()
    {
        if (!TryGetThrowPosition(out Vector3 throwPosition, out Vector3 throwDirection))
            return;

        currentFlashBangs--;
        lastThrowTime = Time.time;

        // Trigger throw animation if available
        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("Throw");
        }

        // Instantiate and throw the flashbang
        GameObject flash = Instantiate(flashBangPrefab, throwPosition, Quaternion.identity);
        ApplyThrowPhysics(flash, throwDirection);

        PlayThrowSound();
        UpdateUI();
        Debug.Log($"Flash bang thrown from {(useRightHand ? "right" : "left")} shoulder! Remaining: {currentFlashBangs}");
    }

    /// <summary>
    /// Applies physics forces to a thrown grenade.
    /// Includes collision ignoring and gravity multiplier.
    /// </summary>
    private void ApplyThrowPhysics(GameObject grenade, Vector3 throwDirection)
    {
        Rigidbody rb = grenade.GetComponent<Rigidbody>();
        Collider grenadeCollider = grenade.GetComponent<Collider>();

        if (rb != null && grenadeCollider != null)
        {
            rb.useGravity = true;
            rb.mass = 1.5f;

            // Temporarily ignore collision with player to prevent self-hit
            Physics.IgnoreCollision(grenadeCollider, playerCollider, true);

            // Apply throw force
            rb.AddForce(throwDirection * throwForce, ForceMode.Impulse);

            // Apply gravity multiplier for realistic arc
            StartCoroutine(ApplyGravityMultiplier(rb));

            // Re-enable collision after short delay
            StartCoroutine(EnablePlayerCollisionAfterDelay(grenadeCollider, 0.2f));
        }
    }

    // ========================================================================
    // THROW POSITION CALCULATION
    // ========================================================================

    /// <summary>
    /// Calculates the throw position and direction.
    /// Includes collision checking to prevent throwing into walls.
    /// </summary>
    private bool TryGetThrowPosition(out Vector3 throwPosition, out Vector3 throwDirection)
    {
        throwPosition = Vector3.zero;
        throwDirection = Vector3.zero;

        Transform throwPoint = GetCurrentThrowPoint();

        if (throwPoint == null)
        {
            Debug.LogError("No throw point assigned in PlayerWeapon!");
            return false;
        }

        // Get throw position from shoulder
        throwPosition = throwPoint.position;

        // Calculate throw direction (camera forward + upward arc)
        if (playerCamera != null)
        {
            Vector3 baseDirection = playerCamera.transform.forward;
            throwDirection = (baseDirection + Vector3.up * upwardForce).normalized;
        }
        else
        {
            throwDirection = (transform.forward + Vector3.up * upwardForce).normalized;
        }

        // Check if spawn position is inside a wall
        if (Physics.CheckSphere(throwPosition, collisionCheckRadius, throwCollisionMask))
        {
            // Try to adjust position slightly forward
            Vector3 adjustedPos = throwPosition + throwDirection * 0.3f;
            if (!Physics.CheckSphere(adjustedPos, collisionCheckRadius, throwCollisionMask))
            {
                throwPosition = adjustedPos;
            }
            else
            {
                Debug.LogWarning("Spawn position at shoulder is obstructed - cannot throw");
            }
        }

        return true;
    }

    // ========================================================================
    // PHYSICS COROUTINES
    // ========================================================================

    /// <summary>
    /// Applies gravity multiplier to create arc trajectory.
    /// Makes grenades drop faster than normal gravity.
    /// </summary>
    private IEnumerator ApplyGravityMultiplier(Rigidbody rb)
    {
        if (rb == null) yield break;

        float elapsedTime = 0;
        while (rb != null && elapsedTime < 5f)
        {
            // Apply extra gravity force
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// Re-enables collision between grenade and player after delay.
    /// Prevents grenade from hitting player immediately after throw.
    /// </summary>
    private IEnumerator EnablePlayerCollisionAfterDelay(Collider grenadeCollider, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (grenadeCollider != null && playerCollider != null)
        {
            Physics.IgnoreCollision(grenadeCollider, playerCollider, false);
        }
    }

    // ========================================================================
    // AUDIO
    // ========================================================================

    private void PlayThrowSound()
    {
        if (throwSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(throwSound, throwVolume);
        }
    }

    // ========================================================================
    // PUBLIC METHODS - AMMO MANAGEMENT
    // ========================================================================

    /// <summary>
    /// Adds smoke bombs to player's inventory (capped at max).
    /// </summary>
    public void AddSmokeBombs(int amount)
    {
        currentSmokeBombs = Mathf.Min(currentSmokeBombs + amount, maxSmokeBombs);
        UpdateUI();
    }

    /// <summary>
    /// Adds flashbangs to player's inventory (capped at max).
    /// </summary>
    public void AddFlashBangs(int amount)
    {
        currentFlashBangs = Mathf.Min(currentFlashBangs + amount, maxFlashBangs);
        UpdateUI();
    }

    /// <summary>
    /// Gets current smoke bomb count.
    /// </summary>
    public int GetSmokeBombCount() => currentSmokeBombs;

    /// <summary>
    /// Gets current flashbang count.
    /// </summary>
    public int GetFlashBangCount() => currentFlashBangs;

    /// <summary>
    /// Switches between right and left throwing hand.
    /// </summary>
    public void SwitchThrowingHand()
    {
        useRightHand = !useRightHand;
        Debug.Log($"Switched to {(useRightHand ? "right" : "left")} shoulder");
    }

    // ========================================================================
    // DEBUG VISUALIZATION
    // ========================================================================

    private void OnDrawGizmosSelected()
    {
        // Draw shoulder spawn points
        if (rightShoulder != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(rightShoulder.position, collisionCheckRadius);
        }

        if (leftShoulder != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(leftShoulder.position, collisionCheckRadius);
        }

        // Draw throw trajectory preview
        if (playerCamera != null && GetCurrentThrowPoint() != null)
        {
            Gizmos.color = Color.green;
            Vector3 direction = (playerCamera.transform.forward + Vector3.up * upwardForce).normalized;
            Gizmos.DrawRay(GetCurrentThrowPoint().position, direction * 3f);
        }
    }
}