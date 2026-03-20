using UnityEngine;
using System.Collections;

public class SmokeBomb : MonoBehaviour
{
    [Header("Smoke Settings")]
    [SerializeField] private float smokeRadius = 5f;
    [SerializeField] private float duration = 8f;
    [SerializeField] private float fadeOutTime = 2f;
    [SerializeField] private ParticleSystem smokeParticles;
    [SerializeField] private Light flashLight;
    [SerializeField] private GameObject areaVisual;

    [Header("Audio")]
    [SerializeField] private AudioClip deploySound;
    [SerializeField] private AudioClip hissSound;

    private float timer;
    private bool isActive = true;
    private AudioSource audioSource;
    private Material areaMaterial;

    void Start()
    {
        timer = duration;

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Play deploy sound
        if (deploySound != null)
            audioSource.PlayOneShot(deploySound);

        // Setup area visual
        if (areaVisual != null)
        {
            areaVisual.transform.localScale = new Vector3(smokeRadius * 2, 0.1f, smokeRadius * 2);
            areaMaterial = areaVisual.GetComponent<Renderer>().material;
            if (areaMaterial != null)
            {
                areaMaterial.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            }
        }

        // Start smoke effect
        if (smokeParticles != null)
            smokeParticles.Play();

        // Affect nearby guards
        AffectGuards(true);

        StartCoroutine(SmokeRoutine());
    }

    void Update()
    {
        if (!isActive) return;

        timer -= Time.deltaTime;

        // Pulse the area visual
        if (areaMaterial != null)
        {
            float alpha = Mathf.PingPong(Time.time * 2f, 0.3f);
            areaMaterial.color = new Color(0.5f, 0.5f, 0.5f, alpha);
        }

        // Check for guards entering smoke
        AffectGuardsInRadius();

        // Fade out particles
        if (timer <= fadeOutTime && smokeParticles != null)
        {
            var emission = smokeParticles.emission;
            emission.rateOverTime = Mathf.Lerp(0, 30, timer / fadeOutTime);

            if (flashLight != null)
                flashLight.intensity = Mathf.Lerp(0, 2, timer / fadeOutTime);
        }
    }

    void AffectGuardsInRadius()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, smokeRadius);

        foreach (Collider col in hitColliders)
        {
            Guard guard = col.GetComponent<Guard>();
            if (guard != null)
            {
                ApplySmokeEffect(guard);
            }
        }
    }

    void AffectGuards(bool addEffect)
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, smokeRadius);

        foreach (Collider col in hitColliders)
        {
            Guard guard = col.GetComponent<Guard>();
            if (guard != null)
            {
                if (addEffect)
                    AddSmokeEffect(guard);
                else
                    RemoveSmokeEffect(guard);
            }
        }
    }

    void ApplySmokeEffect(Guard guard)
    {
        // Check if guard already has modifier
        SmokeModifier modifier = guard.GetComponent<SmokeModifier>();
        if (modifier == null)
        {
            modifier = guard.gameObject.AddComponent<SmokeModifier>();
            modifier.originalDetectionRange = guard.GetDetectionRange();
        }

        // Reduce detection range by 70%
        guard.SetDetectionRange(modifier.originalDetectionRange * 0.3f);
    }

    void AddSmokeEffect(Guard guard)
    {
        SmokeModifier modifier = guard.GetComponent<SmokeModifier>();
        if (modifier == null)
        {
            modifier = guard.gameObject.AddComponent<SmokeModifier>();
            modifier.originalDetectionRange = guard.GetDetectionRange();
        }
        guard.SetDetectionRange(modifier.originalDetectionRange * 0.3f);
    }

    void RemoveSmokeEffect(Guard guard)
    {
        SmokeModifier modifier = guard.GetComponent<SmokeModifier>();
        if (modifier != null)
        {
            guard.SetDetectionRange(modifier.originalDetectionRange);
            Destroy(modifier);
        }
    }

    IEnumerator SmokeRoutine()
    {
        // Play hiss sound
        if (hissSound != null)
        {
            audioSource.PlayOneShot(hissSound);
        }

        yield return new WaitForSeconds(duration);

        // Remove effects from guards
        AffectGuards(false);

        // Fade out
        if (smokeParticles != null)
        {
            var emission = smokeParticles.emission;
            emission.rateOverTime = 0;
        }

        if (areaVisual != null)
        {
            float fadeTime = 0.5f;
            float elapsed = 0;
            Color startColor = areaMaterial.color;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeTime;
                areaMaterial.color = Color.Lerp(startColor, new Color(0.5f, 0.5f, 0.5f, 0), t);
                yield return null;
            }
        }

        yield return new WaitForSeconds(fadeOutTime);

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, smokeRadius);
    }
}

// Helper component
public class SmokeModifier : MonoBehaviour
{
    public float originalDetectionRange;
}