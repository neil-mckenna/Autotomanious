using System.Collections;
using System.Linq;
using UnityEngine;

public class SmokeBomb : MonoBehaviour
{
    [Header("Smoke Settings")]
    [SerializeField] private float smokeRadius = 8f;
    [SerializeField] private float duration = 8f;
    [SerializeField] private float fadeOutTime = 2f;
    [SerializeField] private ParticleSystem smokeParticles;
    [SerializeField] private Light flashLight;
    [SerializeField] private GameObject groundRingPrefab;

    [Header("Detection")]
    [SerializeField] private LayerMask guardLayer = 1 << 8; // Layer 8 for Guards/AI

    [Header("Audio")]
    [SerializeField] private AudioClip deploySound;
    [SerializeField] private AudioClip hissSound;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = true;
    [SerializeField] private Color debugCollisionColor = Color.yellow;
    [SerializeField] private float debugDuration = 2f;

    private AudioSource audioSource;
    private bool hasExploded = false;
    private bool hasLanded = false;
    private GameObject groundRing;
    private Rigidbody rb;
    private Collider bombCollider;

    void Start()
    {
        LogDebug("=== SMOKE BOMB SPAWNED ===");
        LogDebug($"Position: {transform.position}");

        // Set bomb layer
        if (gameObject.layer == 0)
        {
            gameObject.layer = LayerMask.NameToLayer("Default");
        }

        // Setup Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        // Configure Rigidbody for arc throw
        rb.mass = 0.8f;
        rb.linearDamping = 0.3f;
        rb.angularDamping = 0.5f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Prevent rolling
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ;

        // Setup collider
        bombCollider = GetComponent<Collider>();
        if (bombCollider != null)
        {
            bombCollider.isTrigger = false;

            SphereCollider sphereCol = bombCollider as SphereCollider;
            if (sphereCol != null)
            {
                sphereCol.radius = 0.3f;
            }
        }

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Play deploy sound on throw
        if (deploySound != null)
            audioSource.PlayOneShot(deploySound);

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
            if (!hasLanded)
            {
                hasLanded = true;

                // Find exact ground position
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

    void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        // Disable physics
        if (rb != null)
            rb.isKinematic = true;
        if (bombCollider != null)
            bombCollider.enabled = false;

        // Create ground ring
        if (groundRingPrefab != null)
        {
            groundRing = Instantiate(groundRingPrefab, transform.position, Quaternion.identity);
            groundRing.transform.rotation = Quaternion.Euler(90, 0, 0);
            groundRing.transform.localScale = Vector3.one * smokeRadius * 2;

            Collider[] ringColliders = groundRing.GetComponentsInChildren<Collider>();
            foreach (Collider col in ringColliders)
                Destroy(col);

            StartCoroutine(ExpandRing(groundRing, smokeRadius));
        }

        // Setup and play smoke particles
        if (smokeParticles != null)
        {
            smokeParticles.transform.SetParent(null);
            smokeParticles.transform.position = transform.position;

            var main = smokeParticles.main;
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startLifetime = 4f;
            main.maxParticles = 300;

            var emission = smokeParticles.emission;
            emission.rateOverTime = 80;

            var shape = smokeParticles.shape;
            shape.radius = smokeRadius * 0.4f;

            smokeParticles.Play();
        }

        // Play hiss sound
        if (hissSound != null)
            audioSource.PlayOneShot(hissSound);

        // ONLY affect guards on Layer 8
        AffectGuards(true);

        StartCoroutine(FadeOut());
        Destroy(gameObject, 0.5f);
    }

    void AffectGuards(bool addEffect)
    {
        // ONLY check objects on Layer 8 (Guards)
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, smokeRadius, guardLayer);
        LogDebug($"Affecting {hitColliders.Length} guards in smoke radius");

        foreach (Collider col in hitColliders)
        {
            //Debug.LogError($"Found: {col.name}");

            //  Get Guard component
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
                    Debug.LogError($" Applying smoke effects to {brain.GetType().Name}");
                    brain.FlashRedForDuration(0.25f, 6);
                    //brain.Stun(7f); // stun if for flashbang, but was testing
                    brain.Blind(10f, 0.3f);
                    brain.Slow(15f, 0.5f);
                }
                else
                {
                    Debug.LogError($" Restoring {brain.GetType().Name}");
                    brain.RestoreAllEffects();
                }
            }
            else
            {
                //Debug.LogError($" No Guard or currentBrain found on {col.name}");
                // Log what components this object has
                Component[] components = col.GetComponents<Component>();
                Debug.Log($"   Components: {string.Join(", ", components.Select(c => c.GetType().Name))}");
            }
        }
    }

    IEnumerator ExpandRing(GameObject ring, float targetRadius)
    {
        float elapsed = 0f;
        float expandTime = 0.8f;  // Slower expansion
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one * targetRadius * 2;

        Renderer ringRenderer = ring.GetComponent<Renderer>();
        if (ringRenderer != null)
        {
            Color startColor = ringRenderer.material.color;
            startColor.a = 0f;
            ringRenderer.material.color = startColor;
        }

        while (elapsed < expandTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / expandTime;

            // Smooth ease-in-out for smoke
            float smoothT = t * t * (3f - 2f * t);
            ring.transform.localScale = Vector3.Lerp(startScale, endScale, smoothT);

            if (ringRenderer != null)
            {
                Color color = ringRenderer.material.color;
                // Fade in slowly, then fade out
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

    IEnumerator FadeOut()
    {
        yield return new WaitForSeconds(duration);

        // Remove effects from guards
        AffectGuards(false);

        if (smokeParticles != null)
        {
            var emission = smokeParticles.emission;
            emission.rateOverTime = 0;
        }

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

        if (smokeParticles != null)
            Destroy(smokeParticles.gameObject, fadeOutTime);
    }

    void LogDebug(string message)
    {
        if (enableDebug)
            Debug.Log($"[SmokeBomb] {message}");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, smokeRadius);
    }
}

public class SmokeModifier : MonoBehaviour
{
    public float originalDetectionRange;
}