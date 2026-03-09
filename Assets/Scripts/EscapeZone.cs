using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.InputSystem;

public class EscapeZone : MonoBehaviour
{
    #region Properties

    [Header("Escape Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float holdTimeToEscape = 1.5f;
    [SerializeField] private GameObject escapePromptUI;
    [SerializeField] private Slider progressSlider;

    [Header("Effects")]
    [SerializeField] private ParticleSystem victoryEffect;
    [SerializeField] private AudioClip escapeSound;
    [SerializeField] private AudioClip holdSound;

    private bool playerInZone = false;
    private float holdTimer = 0f;
    private AudioSource audioSource;
    private GameManager gameManager;
    private GameObject player;
    private bool isInitialized = false;
    private bool isHolding = false;
    private bool hasEscaped = false;

    private Collider zoneCollider;
    private Rigidbody rb;

    #endregion

    #region Start

    private void Awake()
    {
        // Get components
        zoneCollider = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();

        // Add Rigidbody if missing
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void Start()
    {
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        gameManager = Object.FindAnyObjectByType<GameManager>();

        if (escapePromptUI != null)
            escapePromptUI.SetActive(false);

        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = 0f;
            progressSlider.interactable = false;
        }

        // Reset state
        ResetZone();

        StartCoroutine(FindPlayer());
    }

    private void OnEnable()
    {
        // Reset when enabled (after scene load)
        ResetZone();
    }

    #endregion

    #region Triggers

    private void OnTriggerEnter(Collider other)
    {
        if (hasEscaped) return;

        if (other.CompareTag(playerTag) || other.gameObject == player)
        {
            playerInZone = true;
            Debug.Log("Player entered escape zone");

            if (escapePromptUI != null)
                escapePromptUI.SetActive(true);

            if (progressSlider != null)
                progressSlider.value = 0f;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (hasEscaped) return;

        if (other.CompareTag(playerTag) || other.gameObject == player)
        {
            // Make sure player stays in zone
            playerInZone = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (hasEscaped) return;

        if (other.CompareTag(playerTag) || other.gameObject == player)
        {
            playerInZone = false;
            isHolding = false;
            holdTimer = 0f;

            if (escapePromptUI != null)
                escapePromptUI.SetActive(false);

            if (progressSlider != null)
                progressSlider.value = 0f;

            if (audioSource.isPlaying)
                audioSource.Stop();

            Debug.Log("Player left escape zone");
        }
    }

    #endregion

    #region Player
    private IEnumerator FindPlayer()
    {
        Debug.Log("Looking for player...");

        while (player == null)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);
            if (players != null && players.Length > 0)
                player = players[0];

            if (player == null)
                player = GameObject.Find("Player");

            if (player != null)
            {
                Debug.Log("Player found: " + player.name);
                isInitialized = true;
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    #endregion

    #region Escape, Reset

    private void ResetZone()
    {
        playerInZone = false;
        holdTimer = 0f;
        isHolding = false;
        hasEscaped = false;

        if (zoneCollider != null)
            zoneCollider.enabled = true;

        if (escapePromptUI != null)
            escapePromptUI.SetActive(false);

        if (progressSlider != null)
            progressSlider.value = 0f;

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        Debug.Log("Escape Zone Reset");
    }

    private void Escape()
    {
        // Prevent double escape
        if (hasEscaped) return;
        hasEscaped = true;

        Debug.Log(" PLAYER ESCAPED! VICTORY! ");

        // Stop any playing sounds
        audioSource.Stop();

        // Play victory effects
        if (victoryEffect != null)
            victoryEffect.Play();

        if (escapeSound != null)
            audioSource.PlayOneShot(escapeSound);

        // Hide UI
        if (escapePromptUI != null)
            escapePromptUI.SetActive(false);

        if (progressSlider != null)
            progressSlider.gameObject.SetActive(false);

        // Tell GameManager
        if (gameManager == null)
            gameManager = Object.FindAnyObjectByType<GameManager>();

        if (gameManager != null)
            gameManager.PlayerEscaped();

        // Disable the trigger
        if (zoneCollider != null)
            zoneCollider.enabled = false;

        Debug.Log("Escape sequence complete");
    }

    #endregion

    #region Update

    private void Update()
    {
        if (!isInitialized || player == null || hasEscaped) return;
        if (!playerInZone) return;

        // Check if E is pressed using new Input System
        bool eIsPressed = Keyboard.current != null && Keyboard.current.eKey.isPressed;

        if (eIsPressed)
        {
            // Start holding
            if (!isHolding)
            {
                isHolding = true;
                Debug.Log("Started holding E");
            }

            holdTimer += Time.deltaTime;

            // Play hold sound - but only once, not every frame
            if (holdSound != null && !audioSource.isPlaying)
            {
                audioSource.PlayOneShot(holdSound);
            }

            // Update progress bar
            if (progressSlider != null)
                progressSlider.value = holdTimer / holdTimeToEscape;

            // Check if hold complete
            if (holdTimer >= holdTimeToEscape && !hasEscaped)
            {
                Escape();
            }
        }
        else
        {
            // Only reset if we were holding
            if (isHolding)
            {
                isHolding = false;
                Debug.Log("Stopped holding E - resetting");
            }

            // Progress decays if they stop holding
            if (holdTimer > 0)
            {
                holdTimer = Mathf.Max(0, holdTimer - Time.deltaTime * 2f);

                if (progressSlider != null)
                    progressSlider.value = holdTimer / holdTimeToEscape;
            }

            // Stop hold sound if playing
            if (audioSource.isPlaying && holdSound != null)
            {
                audioSource.Stop();
            }
        }
    }

    #endregion

    #region Debug

    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }

    #endregion
}