using UnityEngine;
using System.Collections;

public class FlashBang : MonoBehaviour
{
    [Header("Flash Settings")]
    [SerializeField] private float flashRadius = 8f;
    [SerializeField] private float stunDuration = 10f;
    [SerializeField] private float lightIntensity = 8f;
    [SerializeField] private Light flashLight;

    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem flashEffect;
    [SerializeField] private ParticleSystem sparkleEffect;
    [SerializeField] private GameObject groundRingPrefab;

    [Header("Detection")]
    [SerializeField] private LayerMask guardLayer = 1 << 8; // Layer 8 for Guards

    [Header("Audio")]
    [SerializeField] private AudioClip bangSound;
    [SerializeField] private float bangVolume = 1f;

    [Header("Physics")]
    [SerializeField] private float groundDetectionDistance = 1.5f;
    [SerializeField] private float timeToExplodeOnGround = 0.5f;
    [SerializeField] private bool disableColliderOnGround = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = true;

    private AudioSource audioSource;
    private bool hasExploded = false;
    private bool hasLanded = false;
    private Rigidbody rb;
    private Collider bombCollider;
    private MeshCollider meshCollider;
    private GameObject groundRing;
    private float groundTouchTime = 0f;

    void Start()
    {
        // Setup Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.mass = 0.5f;
        rb.linearDamping = 0.3f;
        rb.angularDamping = 0.5f;
        rb.useGravity = true;

        // Freeze rotation
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ;

        // Setup collider - specifically get MeshCollider
        bombCollider = GetComponent<Collider>();
        meshCollider = GetComponent<MeshCollider>();

        if (bombCollider != null)
        {
            bombCollider.isTrigger = false;
            LogDebug($"Collider found: {bombCollider.GetType().Name}");
        }

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Start ground detection
        StartCoroutine(GroundDetection());
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;

        // Ignore player and grass
        if (collision.gameObject.CompareTag("Player") ||
            collision.gameObject.CompareTag("Grass"))
            return;

        // Check if we hit the ground
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

    void OnLandedOnGround(Collision collision)
    {
        hasLanded = true;
        LogDebug("Flashbang landed on ground!");

        // Disable MeshCollider if enabled in settings
        if (disableColliderOnGround && meshCollider != null)
        {
            meshCollider.enabled = false;
            LogDebug("MeshCollider disabled on ground impact");
        }

        if (rb != null)
        {
            rb.linearVelocity = rb.linearVelocity * 0.3f;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezePositionY |
                             RigidbodyConstraints.FreezeRotationX |
                             RigidbodyConstraints.FreezeRotationZ;
        }
    }

    void PositionOnGround()
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

    IEnumerator GroundDetection()
    {
        float timeout = 5f;
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

        if (!hasExploded)
        {
            LogDebug("Timeout reached - exploding anyway");
            Explode();
        }
    }

    void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        LogDebug("FLASH BANG EXPLODING!");

        // Disable physics
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // DISABLE THE MESH COLLIDER ON EXPLOSION
        if (meshCollider != null)
        {
            meshCollider.enabled = false;
            LogDebug("MeshCollider disabled on explosion!");
        }

        // Also disable any other colliders
        if (bombCollider != null && bombCollider != meshCollider)
        {
            bombCollider.enabled = false;
        }

        // Create ground ring (using your prefab)
        if (groundRingPrefab != null)
        {
            groundRing = Instantiate(groundRingPrefab, transform.position, Quaternion.identity);
            groundRing.transform.rotation = Quaternion.Euler(90, 0, 0);
            groundRing.transform.localScale = Vector3.one * flashRadius * 2;

            // Remove colliders from ring (visual only)
            Collider[] ringColliders = groundRing.GetComponentsInChildren<Collider>();
            foreach (Collider col in ringColliders)
                Destroy(col);

            // Expand ring effect
            StartCoroutine(ExpandRing(groundRing, flashRadius));
        }

        // Flash light effect
        if (flashLight != null)
        {
            flashLight.intensity = lightIntensity;
            // Create temporary flash light if needed
            GameObject tempLight = new GameObject("TempFlash");
            Light temp = tempLight.AddComponent<Light>();
            temp.type = LightType.Point;
            temp.intensity = lightIntensity;
            temp.range = flashRadius;
            temp.transform.position = transform.position;
            Destroy(tempLight, 0.15f);
            Destroy(flashLight.gameObject, 0.1f);
        }

        // Particle effects
        if (flashEffect != null)
        {
            flashEffect.transform.SetParent(null);
            flashEffect.transform.position = transform.position;
            flashEffect.Play();
            Destroy(flashEffect.gameObject, flashEffect.main.duration);
        }

        if (sparkleEffect != null)
        {
            sparkleEffect.transform.SetParent(null);
            sparkleEffect.transform.position = transform.position;
            sparkleEffect.Play();
            Destroy(sparkleEffect.gameObject, sparkleEffect.main.duration);
        }

        // Play bang sound
        if (bangSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(bangSound, bangVolume);
        }

        // Stun enemies
        StunEnemies();

        // Destroy after effects
        Destroy(gameObject, 2f);
    }

    IEnumerator ExpandRing(GameObject ring, float targetRadius)
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

            // Add a wind force effect - faster expansion
            expandTime = 0.2f;
        }

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

        // Quick fade out after expansion
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

    void StunEnemies()
    {
        // ONLY check objects on Layer 8 (Guards)
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, flashRadius, guardLayer);
        LogDebug($"Found {hitColliders.Length} guards in radius");

        foreach (Collider col in hitColliders)
        {
            LogDebug($"Found: {col.name}");

            // Get Guard component
            Guard guard = col.GetComponent<Guard>();
            if (guard == null)
                guard = col.GetComponentInParent<Guard>();
            if (guard == null)
                guard = col.GetComponentInChildren<Guard>();

            if (guard != null && guard.currentBrain != null)
            {
                AIBrain brain = guard.currentBrain;
                LogDebug($"Stunning {brain.GetType().Name}");

                // Flash Bang effects: Stun + Blind + Quick flash
                brain.FlashRedForDuration(0.1f, 2);
                brain.Stun(stunDuration);
                brain.Blind(stunDuration, 0.1f);
            }
            else
            {
                LogDebug($"No Guard or brain found on {col.name}");
            }
        }
    }

    void LogDebug(string message)
    {
        if (enableDebug)
            Debug.Log($"[FlashBang] {message}");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, flashRadius);

        // Draw ground detection ray
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, Vector3.down * groundDetectionDistance);
    }

    // Public method to manually disable collider (can be called from other scripts)
    public void DisableCollider()
    {
        if (meshCollider != null)
        {
            meshCollider.enabled = false;
            LogDebug("Collider manually disabled");
        }
    }
}