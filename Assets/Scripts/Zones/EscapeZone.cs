using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class EscapeZone : MonoBehaviour
{
    [Header("Escape Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float escapeDelay = 1f;

    private bool isEscaping = false;

    [Header("Effects")]
    [SerializeField] private ParticleSystem victoryEffect;
    [SerializeField] private AudioClip escapeSound;

    private AudioSource audioSource;
    private GameManager gameManager;
    private bool hasEscaped = false;
    private Coroutine escapeCoroutine;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        gameManager = FindAnyObjectByType<GameManager>();
    }

    private void FindGameManager()
    {
        // Try static instance first
        if (GameManager.Instance != null)
        {
            gameManager = GameManager.Instance;
            Debug.Log("EscapeZone found GameManager via Instance");
        }
        else
        {
            // Fallback: find by type
            gameManager = FindAnyObjectByType<GameManager>();
            if (gameManager != null)
                Debug.Log("EscapeZone found GameManager via FindAnyObject");
            else
                Debug.LogWarning("EscapeZone: GameManager not found!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasEscaped || isEscaping) return;

        if (other.CompareTag(playerTag))
        {
            if (gameManager == null)
                FindGameManager();

            isEscaping = true;
            // Start escape coroutine with delay
            if (escapeCoroutine != null)
                StopCoroutine(escapeCoroutine);
            escapeCoroutine = StartCoroutine(EscapeAfterDelay());

            Debug.Log($"Entered escape zone! Escaping in {escapeDelay} seconds...");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            // Cancel escape if player leaves the zone
            if (escapeCoroutine != null)
            {
                StopCoroutine(escapeCoroutine);
                escapeCoroutine = null;

                isEscaping = false;

                Debug.Log("Left escape zone - escape cancelled");
            }
        }
    }

    IEnumerator EscapeAfterDelay()
    {
        yield return new WaitForSeconds(escapeDelay);

        if (!hasEscaped)
        {
            Escape();
        }

        isEscaping = false;
    }

    private void Escape()
    {
        if (hasEscaped) return;
        hasEscaped = true;
        isEscaping = false;

        Debug.Log("PLAYER ESCAPED!");

        if (gameManager == null)
            FindGameManager();

        // Play effects
        if (victoryEffect != null)
            victoryEffect.Play();

        if (escapeSound != null && audioSource != null)
            audioSource.PlayOneShot(escapeSound);

        // Notify GameManager
        if (gameManager != null)
            gameManager.PlayerEscaped();

        // Disable the trigger
        GetComponent<Collider>().enabled = false;

        // Optional: Disable player movement
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
        {
            player.SetActive(false);
        }
    }

    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}