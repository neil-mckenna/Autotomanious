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

    [Header("Debug")]
    [SerializeField] private bool enableDebug = true;

    private AudioSource audioSource;
    private bool hasExploded = false;
    private Rigidbody rb;
    private Collider bombCollider;
    private GameObject groundRing;

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

        // Setup collider
        bombCollider = GetComponent<Collider>();
        if (bombCollider != null)
        {
            bombCollider.isTrigger = false;
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

        // Only explode on ground
        if (collision.gameObject.CompareTag("Ground"))
        {
            if (!hasExploded)
            {
                // Position on ground
                RaycastHit hit;
                if (Physics.Raycast(transform.position, Vector3.down, out hit, 2f))
                {
                    transform.position = new Vector3(transform.position.x, hit.point.y + 0.1f, transform.position.z);
                }

                Explode();
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
            if (Physics.Raycast(transform.position, Vector3.down, out hit, 2f))
            {
                if (hit.collider.CompareTag("Ground") && hit.distance < 0.5f)
                {
                    Explode();
                    yield break;
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        if (!hasExploded)
        {
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
            rb.isKinematic = true;
        if (bombCollider != null)
            bombCollider.enabled = false;

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
            Destroy(flashLight.gameObject, 0.1f);
        }

        // Particle effects
        if (flashEffect != null)
        {
            flashEffect.transform.SetParent(null);
            flashEffect.transform.position = transform.position;
            flashEffect.Play();
        }

        if (sparkleEffect != null)
        {
            sparkleEffect.transform.SetParent(null);
            sparkleEffect.transform.position = transform.position;
            sparkleEffect.Play();
        }

        // Play bang sound
        if (bangSound != null && audioSource != null)
            audioSource.PlayOneShot(bangSound, bangVolume);

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
    }
}