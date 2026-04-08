using UnityEngine;
using System.Collections;

// ============================================================================
// FLASHBANG - THROWABLE GRENADE THAT STUNS AND BLINDS ENEMIES
// ============================================================================
// 
// This script implements a flashbang grenade with the following features:
// 1. Physics-based throwing with gravity and collision detection
// 2. Ground detection with multiple methods (collision + raycast)
// 3. Visual effects (light flash, particle systems, ground ring)
// 4. Audio effects (bang sound)
// 5. Stun + Blind effects on all guards within radius
// 6. MeshCollider disable on ground impact (prevents rolling)
//
// HOW IT WORKS:
// - Throw the grenade (add force to Rigidbody)
// - On ground impact, wait 0.5 seconds then explode
// - Explosion creates light flash, particle effects, and sound
// - All guards in radius get stunned, blinded, and flash red
//
// ============================================================================

public class FlashBang : MonoBehaviour
{
    // ========================================================================
    // SERIALIZED FIELDS - CORE EFFECTS
    // ========================================================================

    [Header("=== FLASH EFFECT SETTINGS ===")]
    [Tooltip("Radius of the flashbang effect (guards within this radius are affected)")]
    [SerializeField] private float flashRadius = 8f;

    [Tooltip("Duration of stun effect on guards (seconds)")]
    [SerializeField] private float stunDuration = 10f;

    [Tooltip("Intensity of the flash light")]
    [SerializeField] private float lightIntensity = 8f;

    [Tooltip("Light component for the flash effect (auto-created if null)")]
    [SerializeField] private Light flashLight;

    [Header("=== PARTICLE EFFECTS ===")]
    [Tooltip("Main flash particle effect (bright burst)")]
    [SerializeField] private ParticleSystem flashEffect;

    [Tooltip("Secondary sparkle particle effect")]
    [SerializeField] private ParticleSystem sparkleEffect;

    [Tooltip("Ground ring prefab for shockwave visualization")]
    [SerializeField] private GameObject groundRingPrefab;

    [Header("=== DETECTION ===")]
    [Tooltip("Layer mask for guards (Layer 8 recommended)")]
    [SerializeField] private LayerMask guardLayer = 1 << 8;  // Layer 8 for Guards

    [Header("=== AUDIO ===")]
    [Tooltip("Bang sound effect played on explosion")]
    [SerializeField] private AudioClip bangSound;

    [Tooltip("Volume of the bang sound (0-1)")]
    [SerializeField] private float bangVolume = 1f;

    [Header("=== PHYSICS SETTINGS ===")]
    [Tooltip("Distance to raycast for ground detection")]
    [SerializeField] private float groundDetectionDistance = 1.5f;

    [Tooltip("Time after landing before explosion (seconds)")]
    [SerializeField] private float timeToExplodeOnGround = 0.5f;

    [Tooltip("Disable MeshCollider when on ground (prevents rolling)")]
    [SerializeField] private bool disableColliderOnGround = true;

    [Header("=== DEBUG ===")]
    [Tooltip("Enable debug logging and gizmo visualization")]
    [SerializeField] private bool enableDebug = true;

    // ========================================================================
    // PRIVATE FIELDS - COMPONENT REFERENCES
    // ========================================================================

    private AudioSource audioSource;      // Audio source for bang sound
    private bool hasExploded = false;     // Prevent multiple explosions
    private bool hasLanded = false;       // Has the grenade touched ground?
    private Rigidbody rb;                 // Rigidbody for physics movement
    private Collider bombCollider;        // Main collider reference
    private MeshCollider meshCollider;    // Mesh collider (if present)
    private GameObject groundRing;        // Reference to instantiated ground ring
    private float groundTouchTime = 0f;   // Time spent touching ground

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    void Start()
    {
        SetupRigidbody();
        SetupColliders();
        SetupAudio();
        StartCoroutine(GroundDetection());
    }

    // ========================================================================
    // INITIALIZATION METHODS
    // ========================================================================

    /// <summary>
    /// Sets up the Rigidbody component for physics-based throwing.
    /// Configures mass, damping, gravity, and rotation constraints.
    /// </summary>
    private void SetupRigidbody()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.mass = 0.5f;
        rb.linearDamping = 0.3f;      // Air resistance
        rb.angularDamping = 0.5f;     // Spin resistance
        rb.useGravity = true;

        // Freeze X and Z rotation to prevent wild spinning
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ;
    }

    /// <summary>
    /// Sets up colliders and detects MeshCollider for special handling.
    /// MeshColliders are disabled on ground impact to prevent rolling.
    /// </summary>
    private void SetupColliders()
    {
        bombCollider = GetComponent<Collider>();
        meshCollider = GetComponent<MeshCollider>();

        if (bombCollider != null)
        {
            bombCollider.isTrigger = false;
            LogDebug($"Collider found: {bombCollider.GetType().Name}");
        }
    }

    /// <summary>
    /// Sets up the AudioSource for playing the bang sound.
    /// </summary>
    private void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    // ========================================================================
    // COLLISION HANDLING
    // ========================================================================

    /// <summary>
    /// Handles initial collision with ground.
    /// Starts the explosion timer when ground is hit.
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;

        // Ignore player and grass collisions
        if (collision.gameObject.CompareTag("Player") ||
            collision.gameObject.CompareTag("Grass"))
            return;

        // Handle ground collision
        if (collision.gameObject.CompareTag("Ground"))
        {
            if (!hasLanded)
            {
                OnLandedOnGround(collision);
            }

            groundTouchTime += Time.deltaTime;

            if (groundTouchTime >= timeToExplodeOnGround && !hasExploded)
            {
                PositionOnGround();
                Explode();
            }
        }
    }

    /// <summary>
    /// Handles continuous ground contact (for when grenade slides/rolls).
    /// </summary>
    void OnCollisionStay(Collision collision)
    {
        if (hasExploded || hasLanded) return;

        if (collision.gameObject.CompareTag("Ground"))
        {
            if (!hasLanded)
            {
                OnLandedOnGround(collision);
            }

            groundTouchTime += Time.deltaTime;

            if (groundTouchTime >= timeToExplodeOnGround && !hasExploded)
            {
                PositionOnGround();
                Explode();
            }
        }
    }

    /// <summary>
    /// Called when grenade first lands on ground.
    /// Disables collider if configured and slows down rigidbody.
    /// </summary>
    private void OnLandedOnGround(Collision collision)
    {
        hasLanded = true;
        LogDebug("Flashbang landed on ground!");

        // Disable MeshCollider to prevent rolling
        if (disableColliderOnGround && meshCollider != null)
        {
            meshCollider.enabled = false;
            LogDebug("MeshCollider disabled on ground impact");
        }

        // Slow down the grenade significantly
        if (rb != null)
        {
            rb.linearVelocity = rb.linearVelocity * 0.3f;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezePositionY |
                             RigidbodyConstraints.FreezeRotationX |
                             RigidbodyConstraints.FreezeRotationZ;
        }
    }

    /// <summary>
    /// Snaps the grenade to the exact ground position.
    /// Prevents floating or clipping through terrain.
    /// </summary>
    private void PositionOnGround()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, groundDetectionDistance))
        {
            if (hit.collider.CompareTag("Ground"))
            {
                transform.position = new Vector3(transform.position.x, hit.point.y + 0.05f, transform.position.z);
                LogDebug($"Positioned on ground at y={transform.position.y}");
            }
        }
    }

    // ========================================================================
    // GROUND DETECTION (Fallback for missed collisions)
    // ========================================================================

    /// <summary>
    /// Coroutine that continuously raycasts downward to detect ground.
    /// Acts as a fallback if collision detection fails.
    /// </summary>
    private IEnumerator GroundDetection()
    {
        float timeout = 5f;  // Maximum time to wait for ground
        float timer = 0f;

        while (!hasExploded && timer < timeout)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit, groundDetectionDistance))
            {
                if (hit.collider.CompareTag("Ground"))
                {
                    if (!hasLanded)
                    {
                        hasLanded = true;
                        LogDebug("Ground detected via raycast!");

                        if (disableColliderOnGround && meshCollider != null)
                        {
                            meshCollider.enabled = false;
                            LogDebug("MeshCollider disabled via raycast");
                        }
                    }

                    groundTouchTime += Time.deltaTime;

                    if (groundTouchTime >= timeToExplodeOnGround && !hasExploded)
                    {
                        transform.position = new Vector3(transform.position.x, hit.point.y + 0.05f, transform.position.z);
                        Explode();
                        yield break;
                    }
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // Explode anyway after timeout (prevents infinite hanging)
        if (!hasExploded)
        {
            LogDebug("Timeout reached - exploding anyway");
            Explode();
        }
    }

    // ========================================================================
    // EXPLOSION EFFECTS
    // ========================================================================

    /// <summary>
    /// Main explosion method - creates all visual and audio effects.
    /// Disables physics, creates light flash, particles, ground ring, and stuns enemies.
    /// </summary>
    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        LogDebug("FLASH BANG EXPLODING!");

        // Disable physics components
        DisablePhysics();

        // Create ground ring (shockwave visualization)
        CreateGroundRing();

        // Create light flash effect
        CreateFlashLight();

        // Create particle effects
        CreateParticleEffects();

        // Play bang sound
        PlayBangSound();

        // Stun all guards in radius
        StunEnemies();

        // Destroy the grenade after effects complete
        Destroy(gameObject, 2f);
    }

    /// <summary>
    /// Disables Rigidbody and colliders to prevent further physics interactions.
    /// </summary>
    private void DisablePhysics()
    {
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Disable MeshCollider on explosion
        if (meshCollider != null)
        {
            meshCollider.enabled = false;
            LogDebug("MeshCollider disabled on explosion!");
        }

        // Disable any other colliders
        if (bombCollider != null && bombCollider != meshCollider)
        {
            bombCollider.enabled = false;
        }
    }

    /// <summary>
    /// Creates the ground ring effect (shockwave visualization).
    /// </summary>
    private void CreateGroundRing()
    {
        if (groundRingPrefab != null)
        {
            groundRing = Instantiate(groundRingPrefab, transform.position, Quaternion.identity);
            groundRing.transform.rotation = Quaternion.Euler(90, 0, 0);  // Lay flat on ground
            groundRing.transform.localScale = Vector3.one * flashRadius * 2;

            // Remove colliders from ring (visual only)
            Collider[] ringColliders = groundRing.GetComponentsInChildren<Collider>();
            foreach (Collider col in ringColliders)
                Destroy(col);

            // Start expansion animation
            StartCoroutine(ExpandRing(groundRing, flashRadius));
        }
    }

    /// <summary>
    /// Creates the bright flash light effect.
    /// </summary>
    private void CreateFlashLight()
    {
        if (flashLight != null)
        {
            flashLight.intensity = lightIntensity;

            // Create temporary flash light for brighter effect
            GameObject tempLight = new GameObject("TempFlash");
            Light temp = tempLight.AddComponent<Light>();
            temp.type = LightType.Point;
            temp.intensity = lightIntensity;
            temp.range = flashRadius;
            temp.transform.position = transform.position;
            Destroy(tempLight, 0.15f);
            Destroy(flashLight.gameObject, 0.1f);
        }
    }

    /// <summary>
    /// Creates and plays particle effects.
    /// </summary>
    private void CreateParticleEffects()
    {
        // Main flash effect
        if (flashEffect != null)
        {
            flashEffect.transform.SetParent(null);
            flashEffect.transform.position = transform.position;
            flashEffect.Play();
            Destroy(flashEffect.gameObject, flashEffect.main.duration);
        }

        // Sparkle effect
        if (sparkleEffect != null)
        {
            sparkleEffect.transform.SetParent(null);
            sparkleEffect.transform.position = transform.position;
            sparkleEffect.Play();
            Destroy(sparkleEffect.gameObject, sparkleEffect.main.duration);
        }
    }

    /// <summary>
    /// Plays the bang sound effect.
    /// </summary>
    private void PlayBangSound()
    {
        if (bangSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(bangSound, bangVolume);
        }
    }

    // ========================================================================
    // RING EXPANSION ANIMATION
    // ========================================================================

    /// <summary>
    /// Coroutine that animates the ground ring expanding outward.
    /// Creates a shockwave effect that fades out over time.
    /// </summary>
    private IEnumerator ExpandRing(GameObject ring, float targetRadius)
    {
        float elapsed = 0f;
        float expandTime = 0.25f;
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one * targetRadius * 2;

        Renderer ringRenderer = ring.GetComponent<Renderer>();
        if (ringRenderer != null)
        {
            // Start fully transparent
            Color startColor = ringRenderer.material.color;
            startColor.a = 0f;
            ringRenderer.material.color = startColor;

            // Faster expansion for shockwave effect
            expandTime = 0.2f;
        }

        // Expansion phase
        while (elapsed < expandTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / expandTime;

            // Ease out for wind burst effect (starts fast, slows down)
            float easeOut = 1f - Mathf.Pow(1f - t, 2f);
            ring.transform.localScale = Vector3.Lerp(startScale, endScale, easeOut);

            if (ringRenderer != null)
            {
                Color color = ringRenderer.material.color;
                // Fade in quickly, then fade out for shockwave effect
                if (t < 0.3f)
                    color.a = Mathf.Lerp(0f, 0.6f, t / 0.3f);
                else
                    color.a = Mathf.Lerp(0.6f, 0f, (t - 0.3f) / 0.7f);

                ringRenderer.material.color = color;
            }
            yield return null;
        }

        // Fade out phase
        if (ringRenderer != null)
        {
            float fadeTime = 0.3f;
            float fadeElapsed = 0f;
            Color startColor = ringRenderer.material.color;

            while (fadeElapsed < fadeTime)
            {
                fadeElapsed += Time.deltaTime;
                float t = fadeElapsed / fadeTime;
                Color color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, t);
                ringRenderer.material.color = color;
                yield return null;
            }
        }

        Destroy(ring);
    }

    // ========================================================================
    // ENEMY EFFECTS
    // ========================================================================

    /// <summary>
    /// Finds all guards within flash radius and applies effects:
    /// - Stun (stops movement)
    /// - Blind (reduces detection range)
    /// - Flash red (visual feedback)
    /// </summary>
    private void StunEnemies()
    {
        // Find all guards on Layer 8 within radius
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, flashRadius, guardLayer);
        LogDebug($"Found {hitColliders.Length} guards in radius");

        foreach (Collider col in hitColliders)
        {
            LogDebug($"Found: {col.name}");

            // Get Guard component (try multiple methods)
            Guard guard = col.GetComponent<Guard>();
            if (guard == null)
                guard = col.GetComponentInParent<Guard>();
            if (guard == null)
                guard = col.GetComponentInChildren<Guard>();

            if (guard != null && guard.currentBrain != null)
            {
                AIBrain brain = guard.currentBrain;
                LogDebug($"Stunning {brain.GetType().Name}");

                // Apply all flashbang effects
                brain.FlashRedForDuration(0.1f, 2);  // Quick red flash
                brain.Stun(stunDuration);             // Stop movement
                brain.Blind(stunDuration, 0.1f);      // Reduce vision to 10%
            }
            else
            {
                LogDebug($"No Guard or brain found on {col.name}");
            }
        }
    }

    // ========================================================================
    // DEBUG UTILITIES
    // ========================================================================

    /// <summary>
    /// Logs debug messages when enableDebug is true.
    /// </summary>
    private void LogDebug(string message)
    {
        if (enableDebug)
            Debug.Log($"[FlashBang] {message}");
    }

    /// <summary>
    /// Draws gizmos in editor when object is selected.
    /// Shows flash radius and ground detection ray.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Flash radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, flashRadius);

        // Ground detection ray
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, Vector3.down * groundDetectionDistance);
    }

    // ========================================================================
    // PUBLIC METHODS
    // ========================================================================

    /// <summary>
    /// Public method to manually disable the collider.
    /// Can be called from other scripts if needed.
    /// </summary>
    public void DisableCollider()
    {
        if (meshCollider != null)
        {
            meshCollider.enabled = false;
            LogDebug("Collider manually disabled");
        }
    }
}