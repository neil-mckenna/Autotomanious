using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

// ============================================================================
// ESCAPE ZONE - LEVEL EXIT TRIGGER WITH DELAYED ESCAPE MECHANIC
// ============================================================================
// 
// This script handles the level exit/escape zone functionality.
// When the player enters the zone and stays for a set duration, they escape.
//
// FEATURES:
// 1. Delayed escape (prevents accidental triggers)
// 2. Visual and audio feedback on escape
// 3. Cancellable escape (player can leave zone)
// 4. GameManager integration for stats tracking
// 5. Disables player on escape (prevents post-game movement)
//
// ESCAPE SEQUENCE:
// 1. Player enters trigger zone
// 2. Escape timer starts (escapeDelay seconds)
// 3. If player stays in zone, escape triggers
// 4. If player leaves, escape cancels
// 5. On escape: effects play, GameManager notified, player disabled
//
// ============================================================================

public class EscapeZone : MonoBehaviour
{
    // ========================================================================
    // SERIALIZED FIELDS - ESCAPE SETTINGS
    // ========================================================================

    [Header("=== ESCAPE SETTINGS ===")]
    [Tooltip("Tag of the player object")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("How long player must stay in zone to escape (seconds)")]
    [SerializeField] private float escapeDelay = 1f;

    // ========================================================================
    // SERIALIZED FIELDS - VISUAL & AUDIO EFFECTS
    // ========================================================================

    [Header("=== EFFECTS ===")]
    [Tooltip("Particle effect to play on escape")]
    [SerializeField] private ParticleSystem victoryEffect;

    [Tooltip("Sound to play on escape")]
    [SerializeField] private AudioClip escapeSound;

    // ========================================================================
    // PRIVATE FIELDS
    // ========================================================================

    private bool isEscaping = false;      // Is escape sequence active?
    private AudioSource audioSource;      // Audio source for escape sound
    private GameManager gameManager;      // Reference to GameManager
    private bool hasEscaped = false;      // Prevent multiple escapes
    private Coroutine escapeCoroutine;    // Reference to escape coroutine

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    private void Start()
    {
        SetupAudioSource();
        FindGameManager();
    }

    // ========================================================================
    // INITIALIZATION METHODS
    // ========================================================================

    /// <summary>
    /// Sets up the audio source component for playing escape sound.
    /// </summary>
    private void SetupAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    /// <summary>
    /// Finds the GameManager instance for stats tracking.
    /// Tries static Instance first, then falls back to FindAnyObject.
    /// </summary>
    private void FindGameManager()
    {
        // Try static instance first (fastest)
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

    // ========================================================================
    // TRIGGER HANDLING
    // ========================================================================

    /// <summary>
    /// Called when something enters the trigger zone.
    /// Starts the escape countdown if player enters.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (hasEscaped || isEscaping) return;

        if (other.CompareTag(playerTag))
        {
            // Ensure GameManager reference exists
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

    /// <summary>
    /// Called when something exits the trigger zone.
    /// Cancels the escape countdown if player leaves.
    /// </summary>
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

    // ========================================================================
    // ESCAPE SEQUENCE
    // ========================================================================

    /// <summary>
    /// Coroutine that waits for the escape delay then triggers escape.
    /// </summary>
    private IEnumerator EscapeAfterDelay()
    {
        yield return new WaitForSeconds(escapeDelay);

        if (!hasEscaped)
        {
            Escape();
        }

        isEscaping = false;
    }

    /// <summary>
    /// Performs the actual escape sequence.
    /// Plays effects, notifies GameManager, and disables player.
    /// </summary>
    private void Escape()
    {
        if (hasEscaped) return;
        hasEscaped = true;
        isEscaping = false;

        //Debug.Log("PLAYER ESCAPED!");

        // Ensure GameManager reference exists
        if (gameManager == null)
            FindGameManager();

        // Play visual effects
        if (victoryEffect != null)
            victoryEffect.Play();

        // Play audio effect
        if (escapeSound != null && audioSource != null)
            audioSource.PlayOneShot(escapeSound);

        // Notify GameManager for stats tracking
        if (gameManager != null)
            gameManager.PlayerEscaped();

        // Disable the trigger to prevent multiple escapes
        GetComponent<Collider>().enabled = false;

        // Disable player movement (prevent post-game movement)
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
        {
            player.SetActive(false);
        }
    }

    // ========================================================================
    // DEBUG VISUALIZATION
    // ========================================================================

    /// <summary>
    /// Draws the escape zone bounds in the Scene view for debugging.
    /// </summary>
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