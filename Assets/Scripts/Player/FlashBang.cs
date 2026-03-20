using UnityEngine;
using System.Collections;

public class FlashBang : MonoBehaviour
{
    [Header("Flash Settings")]
    [SerializeField] private float flashRadius = 8f;
    [SerializeField] private float stunDuration = 3f;
    [SerializeField] private float lightIntensity = 8f;
    [SerializeField] private Light flashLight;

    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem flashEffect;
    [SerializeField] private ParticleSystem ringEffect;
    [SerializeField] private ParticleSystem sparkleEffect;

    [Header("Audio")]
    [SerializeField] private AudioClip bangSound;
    [SerializeField] private float bangVolume = 1f;

    [Header("Camera Shake")]
    [SerializeField] private float shakeDuration = 0.3f;
    [SerializeField] private float shakeMagnitude = 0.5f;

    private AudioSource audioSource;
    private Camera mainCamera;

    void Start()
    {
        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Find main camera
        mainCamera = Camera.main;

        // Play effects
        PlayFlashEffects();

        // Stun nearby enemies
        StunEnemies();

        // Destroy after effects
        Destroy(gameObject, 2f);
    }

    void PlayFlashEffects()
    {
        // Flash light
        if (flashLight != null)
        {
            flashLight.intensity = lightIntensity;
            // Destroy the light after a short delay
            Destroy(flashLight.gameObject, 0.1f);
        }

        // Play particle effects
        if (flashEffect != null)
            flashEffect.Play();

        if (ringEffect != null)
            ringEffect.Play();

        if (sparkleEffect != null)
            sparkleEffect.Play();

        // Play bang sound
        if (bangSound != null && audioSource != null)
            audioSource.PlayOneShot(bangSound, bangVolume);

        // Camera shake
        StartCoroutine(CameraShake());
    }

    void StunEnemies()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, flashRadius);

        foreach (Collider col in hitColliders)
        {
            // Stun Guards
            Guard guard = col.GetComponent<Guard>();
            if (guard != null)
            {
                StunGuard(guard);
            }

            // Stun Zombies
            Zombie zombie = col.GetComponent<Zombie>();
            if (zombie != null)
            {
                zombie.Stun(stunDuration);
                Debug.Log($" Zombie stunned!");
            }
        }
    }

    void StunGuard(Guard guard)
    {
        float originalRange = guard.GetDetectionRange();
        guard.SetDetectionRange(0.5f);

        // Store original to restore later
        FlashModifier modifier = guard.GetComponent<FlashModifier>();
        if (modifier == null)
        {
            modifier = guard.gameObject.AddComponent<FlashModifier>();
            modifier.originalDetectionRange = originalRange;
        }

        StartCoroutine(RestoreGuard(guard, modifier));
        Debug.Log($" Guard {guard.name} stunned!");
    }

    IEnumerator RestoreGuard(Guard guard, FlashModifier modifier)
    {
        yield return new WaitForSeconds(stunDuration);

        if (guard != null && modifier != null)
        {
            guard.SetDetectionRange(modifier.originalDetectionRange);
            Destroy(modifier);
            Debug.Log($" Guard {guard.name} vision restored");
        }
    }

    IEnumerator CameraShake()
    {
        if (mainCamera == null) yield break;

        Vector3 originalPos = mainCamera.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-shakeMagnitude, shakeMagnitude);
            float y = Random.Range(-shakeMagnitude * 0.5f, shakeMagnitude * 0.5f);

            mainCamera.transform.localPosition = originalPos + new Vector3(x, y, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.localPosition = originalPos;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, flashRadius);
    }
}

// Helper component
public class FlashModifier : MonoBehaviour
{
    public float originalDetectionRange;
}