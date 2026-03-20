using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class EscapeZone : MonoBehaviour
{
    [Header("Escape Settings")]
    [SerializeField] private string playerTag = "Player";

    [Header("Effects")]
    [SerializeField] private ParticleSystem victoryEffect;
    [SerializeField] private AudioClip escapeSound;

    private AudioSource audioSource;
    private GameManager gameManager;
    [SerializeField] public bool hasEscaped = false;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        gameManager = GameManager.Instance;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        gameManager = GameManager.Instance;
    }

    private void OnEnable()
    {
        // Reset when the object becomes active (after scene reload)
        hasEscaped = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasEscaped) return;

        if (other.CompareTag(playerTag))
        {
            hasEscaped = true;
            Debug.Log("Player escaped!");

            // Play effects
            if (victoryEffect != null)
                victoryEffect.Play();

            if (escapeSound != null && audioSource != null)
                audioSource.PlayOneShot(escapeSound);

            // Notify GameManager
            if (gameManager != null)
                gameManager.PlayerEscaped();
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