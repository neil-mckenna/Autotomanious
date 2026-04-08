using System.Collections;
using System.Linq;
using UnityEngine;

// ============================================================================
// SMOKE BOMB - AREA DENIAL GRENADE THAT BLINDS AND SLOWS ENEMIES
// ============================================================================
// 
// This script implements a smoke bomb grenade with the following features:
// 1. Physics-based throwing with gravity and collision detection
// 2. Creates a persistent smoke cloud that lasts for duration
// 3. Affects guards within smoke radius (Blind + Slow effects)
// 4. Visual effects (smoke particles, ground ring)
// 5. Audio effects (deploy sound, hissing smoke)
// 6. Effects persist while smoke is active, then restore guards
//
// DIFFERENCES FROM FLASHBANG:
// - Smoke lasts longer (8 seconds vs instant)
// - Effects are Blind + Slow (not Stun)
// - Gradual fade in/out of effects
// - Smoke particles persist and linger
//
// HOW IT WORKS:
// - Throw the smoke bomb (add force to Rigidbody)
// - On ground impact, deploy smoke cloud
// - Guards in radius get Blinded and Slowed
// - After duration, effects are removed from guards
//
// ============================================================================

public class SmokeBomb : MonoBehaviour
{
    // ========================================================================
    // SERIALIZED FIELDS - CORE EFFECTS
    // ========================================================================

    [Header("=== SMOKE EFFECT SETTINGS ===")]
    [Tooltip("Radius of the smoke cloud effect")]
    [SerializeField] private float smokeRadius = 8f;

    [Tooltip("How long the smoke lasts (seconds)")]
    [SerializeField] private float duration = 8f;

    [Tooltip("Time for smoke to fade out at end (seconds)")]
    [SerializeField] private float fadeOutTime = 2f;

    [Tooltip("Particle system for smoke effect")]
    [SerializeField] private ParticleSystem smokeParticles;

    [Tooltip("Light for brief flash on deployment (optional)")]
    [SerializeField] private Light flashLight;

    [Tooltip("Ground ring prefab for smoke area visualization")]
    [SerializeField] private GameObject groundRingPrefab;

    [Header("=== DETECTION ===")]
    [Tooltip("Layer mask for guards (Layer 8 recommended)")]
    [SerializeField] private LayerMask guardLayer = 1 << 8;  // Layer 8 for Guards/AI

    [Header("=== AUDIO ===")]
    [Tooltip("Sound played when smoke bomb is thrown")]
    [SerializeField] private AudioClip deploySound;

    [Tooltip("Hissing sound of smoke deploying")]
    [SerializeField] private AudioClip hissSound;

    [Header("=== DEBUG ===")]
    [Tooltip("Enable debug logging")]
    [SerializeField] private bool enableDebug = true;

    [Tooltip("Color for debug collision visualization")]
    [SerializeField] private Color debugCollisionColor = Color.yellow;

    [Tooltip("Duration of debug visualization")]
    [SerializeField] private float debugDuration = 2f;

    // ========================================================================
    // PRIVATE FIELDS - COMPONENT REFERENCES
    // ========================================================================

    private AudioSource audioSource;      // Audio source for sounds
    private bool hasExploded = false;     // Prevent multiple explosions
    private bool hasLanded = false;       // Has the smoke bomb touched ground?
    private GameObject groundRing;        // Reference to instantiated ground ring
    private Rigidbody rb;                 // Rigidbody for physics movement
    private Collider bombCollider;        // Main collider reference

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    void Start()
    {
        LogDebug("=== SMOKE BOMB SPAWNED ===");
        LogDebug($"Position: {transform.position}");

        // Set bomb layer if not assigned
        if (gameObject.layer == 0)
        {
            gameObject.layer = LayerMask.NameToLayer("Default");
        }

        SetupRigidbody();
        SetupCollider();
        SetupAudio();

        // Play deploy sound on throw
        if (deploySound != null)
            audioSource.PlayOneShot(deploySound);

        // Start ground detection coroutine
        StartCoroutine(GroundDetection());
    }

    // ========================================================================
    // INITIALIZATION METHODS
    // ========================================================================

    /// <summary>
    /// Sets up the Rigidbody component for physics-based throwing.
    /// Configures mass, damping, gravity, and collision detection.
    /// </summary>
    private void SetupRigidbody()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        // Configure for arc throw
        rb.mass = 0.8f;
        rb.linearDamping = 0.3f;      // Air resistance
        rb.angularDamping = 0.5f;     // Spin resistance
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;  // Prevent fast collisions

        // Freeze X and Z rotation to prevent wild rolling
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ;
    }

    /// <summary>
    /// Sets up the collider for ground detection.
    /// Configures sphere collider radius if present.
    /// </summary>
    private void SetupCollider()
    {
        bombCollider = GetComponent<Collider>();
        if (bombCollider != null)
        {
            bombCollider.isTrigger = false;

            // Configure sphere collider if present
            SphereCollider sphereCol = bombCollider as SphereCollider;
            if (sphereCol != null)
            {
                sphereCol.radius = 0.3f;
            }
        }
    }

    /// <summary>
    /// Sets up the AudioSource for playing sounds.
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
    /// Handles collision with ground.
    /// Explodes immediately on ground impact.
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;

        // Ignore player and grass collisions
        if (collision.gameObject.CompareTag("Player") ||
            collision.gameObject.CompareTag("Grass"))
            return;

        // Only explode on ground
        if (collision.gameObject.CompareTag("Ground"))
        {
            if (!hasLanded)
            {
                hasLanded = true;

                // Snap to exact ground position
                RaycastHit hit;
                if (Physics.Raycast(transform.position, Vector3.down, out hit, 2f))
                {
                    transform.position = new Vector3(transform.position.x, hit.point.y + 0.1f, transform.position.z);
                }

                Explode();
            }
        }
    }

    // ========================================================================
    // GROUND DETECTION (Fallback for missed collisions)
    // ========================================================================

    /// <summary>
    /// Coroutine that continuously raycasts downward to detect ground.
    /// Acts as a fallback if collision detection fails.
    /// Includes timeout to prevent infinite waiting.
    /// </summary>
    private IEnumerator GroundDetection()
    {
        float timeout = 5f;  // Maximum time to wait for ground
        float timer = 0f;

        while (!hasLanded && timer < timeout)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit, 2f))
            {
                if (hit.collider.CompareTag("Ground") && hit.distance < 0.5f)
                {
                    hasLanded = true;
                    transform.position = new Vector3(transform.position.x, hit.point.y + 0.1f, transform.position.z);
                    Explode();
                    yield break;
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // Explode anyway after timeout (prevents infinite hanging)
        if (!hasExploded)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit, 10f))
            {
                transform.position = new Vector3(transform.position.x, hit.point.y + 0.1f, transform.position.z);
            }
            Explode();
        }
    }

    // ========================================================================
    // EXPLOSION / DEPLOYMENT
    // ========================================================================

    /// <summary>
    /// Main explosion method - deploys the smoke cloud.
    /// Disables physics, creates visual effects, and applies debuffs to guards.
    /// </summary>
    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        LogDebug("SMOKE BOMB DEPLOYED!");

        // Disable physics components
        DisablePhysics();

        // Create ground ring (smoke area visualization)
        CreateGroundRing();

        // Setup and play smoke particles
        SetupSmokeParticles();

        // Play hissing sound
        PlayHissSound();

        // Apply debuffs to all guards in radius
        AffectGuards(true);

        // Start fade out coroutine
        StartCoroutine(FadeOut());

        // Destroy the smoke bomb GameObject
        Destroy(gameObject, 0.5f);
    }

    /// <summary>
    /// Disables Rigidbody and collider to prevent further physics interactions.
    /// </summary>
    private void DisablePhysics()
    {
        if (rb != null)
            rb.isKinematic = true;
        if (bombCollider != null)
            bombCollider.enabled = false;
    }

    /// <summary>
    /// Creates the ground ring effect (smoke area visualization).
    /// </summary>
    private void CreateGroundRing()
    {
        if (groundRingPrefab != null)
        {
            groundRing = Instantiate(groundRingPrefab, transform.position, Quaternion.identity);
            groundRing.transform.rotation = Quaternion.Euler(90, 0, 0);  // Lay flat on ground
            groundRing.transform.localScale = Vector3.one * smokeRadius * 2;

            // Remove colliders from ring (visual only)
            Collider[] ringColliders = groundRing.GetComponentsInChildren<Collider>();
            foreach (Collider col in ringColliders)
                Destroy(col);

            // Start expansion animation
            StartCoroutine(ExpandRing(groundRing, smokeRadius));
        }
    }

    /// <summary>
    /// Configures and plays the smoke particle system.
    /// Adjusts particle size, lifetime, emission rate, and shape.
    /// </summary>
    private void SetupSmokeParticles()
    {
        if (smokeParticles != null)
        {
            // Detach from smoke bomb (since bomb will be destroyed)
            smokeParticles.transform.SetParent(null);
            smokeParticles.transform.position = transform.position;

            // Configure particle system for thick smoke
            var main = smokeParticles.main;
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startLifetime = 4f;
            main.maxParticles = 300;

            var emission = smokeParticles.emission;
            emission.rateOverTime = 80;

            var shape = smokeParticles.shape;
            shape.radius = smokeRadius * 0.4f;

            // Start emitting smoke
            smokeParticles.Play();
        }
    }

    /// <summary>
    /// Plays the hissing sound of the smoke bomb.
    /// </summary>
    private void PlayHissSound()
    {
        if (hissSound != null)
            audioSource.PlayOneShot(hissSound);
    }

    // ========================================================================
    // GUARD EFFECTS
    // ========================================================================

    /// <summary>
    /// Applies or removes smoke effects to all guards within radius.
    /// Effects applied: Blind (reduced vision) + Slow (reduced speed)
    /// </summary>
    /// <param name="addEffect">True to apply effects, false to remove</param>
    private void AffectGuards(bool addEffect)
    {
        // Find all guards on Layer 8 within smoke radius
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, smokeRadius, guardLayer);
        LogDebug($"Affecting {hitColliders.Length} guards in smoke radius");

        foreach (Collider col in hitColliders)
        {
            // Get Guard component (try multiple methods)
            Guard guard = col.GetComponent<Guard>();
            if (guard == null)
                guard = col.GetComponentInParent<Guard>();
            if (guard == null)
                guard = col.GetComponentInChildren<Guard>();

            if (guard != null && guard.currentBrain != null)
            {
                AIBrain brain = guard.currentBrain;

                if (addEffect)
                {
                    // Apply smoke effects
                    LogDebug($"Applying smoke effects to {brain.GetType().Name}");
                    brain.FlashRedForDuration(0.25f, 6);  // Visual feedback
                    brain.Blind(10f, 0.3f);               // Reduce vision to 30%
                    brain.Slow(15f, 0.5f);                // Reduce speed to 50%
                }
                else
                {
                    // Remove all effects when smoke clears
                    LogDebug($"Restoring {brain.GetType().Name}");
                    brain.RestoreAllEffects();
                }
            }
            else
            {
                // Debug: Log components if guard not found
                Component[] components = col.GetComponents<Component>();
                LogDebug($"No Guard or brain found on {col.name}. Components: {string.Join(", ", components.Select(c => c.GetType().Name))}");
            }
        }
    }

    // ========================================================================
    // RING EXPANSION ANIMATION
    // ========================================================================

    /// <summary>
    /// Coroutine that animates the ground ring expanding outward.
    /// Creates a smoke cloud spreading effect that fades in then out.
    /// </summary>
    private IEnumerator ExpandRing(GameObject ring, float targetRadius)
    {
        float elapsed = 0f;
        float expandTime = 0.8f;  // Slower expansion for smoke (vs flashbang)
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one * targetRadius * 2;

        Renderer ringRenderer = ring.GetComponent<Renderer>();
        if (ringRenderer != null)
        {
            // Start fully transparent
            Color startColor = ringRenderer.material.color;
            startColor.a = 0f;
            ringRenderer.material.color = startColor;
        }

        // Expansion phase
        while (elapsed < expandTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / expandTime;

            // Smooth ease-in-out for smoke (gentler than flashbang)
            float smoothT = t * t * (3f - 2f * t);
            ring.transform.localScale = Vector3.Lerp(startScale, endScale, smoothT);

            if (ringRenderer != null)
            {
                Color color = ringRenderer.material.color;
                // Fade in slowly, then fade out for smoke effect
                if (t < 0.4f)
                    color.a = Mathf.Lerp(0f, 0.4f, t / 0.4f);
                else
                    color.a = Mathf.Lerp(0.4f, 0f, (t - 0.4f) / 0.6f);

                ringRenderer.material.color = color;
            }
            yield return null;
        }

        Destroy(ring);
    }

    // ========================================================================
    // FADE OUT (Cleanup)
    // ========================================================================

    /// <summary>
    /// Coroutine that handles the smoke cloud fading out.
    /// Removes effects from guards and cleans up particles.
    /// </summary>
    private IEnumerator FadeOut()
    {
        // Wait for the smoke duration
        yield return new WaitForSeconds(duration);

        LogDebug("Smoke clearing - removing effects from guards");

        // Remove all smoke effects from guards
        AffectGuards(false);

        // Stop smoke particle emission
        if (smokeParticles != null)
        {
            var emission = smokeParticles.emission;
            emission.rateOverTime = 0;
        }

        // Fade out the ground ring
        if (groundRing != null)
        {
            Renderer ringRenderer = groundRing.GetComponent<Renderer>();
            if (ringRenderer != null)
            {
                float elapsed = 0f;
                Color startColor = ringRenderer.material.color;

                while (elapsed < fadeOutTime)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / fadeOutTime;
                    ringRenderer.material.color = Color.Lerp(startColor, new Color(startColor.r, startColor.g, startColor.b, 0), t);
                    yield return null;
                }
            }
            Destroy(groundRing);
        }

        // Destroy smoke particles after fade out
        if (smokeParticles != null)
            Destroy(smokeParticles.gameObject, fadeOutTime);
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
            Debug.Log($"[SmokeBomb] {message}");
    }

    /// <summary>
    /// Draws gizmos in editor when object is selected.
    /// Shows smoke radius for placement debugging.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, smokeRadius);
    }
}

// ============================================================================
// SMOKE MODIFIER - HELPER COMPONENT FOR SMOKE EFFECTS
// ============================================================================
// 
// This component can be attached to guards to track and restore
// original detection range after smoke effects wear off.
// (Currently not fully implemented - kept for future expansion)
//
// ============================================================================

/// <summary>
/// Helper component for smoke effect modification tracking.
/// Stores original detection range for restoration after smoke clears.
/// </summary>
public class SmokeModifier : MonoBehaviour
{
    [Tooltip("Original detection range before smoke effect")]
    public float originalDetectionRange;
}