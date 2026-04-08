using UnityEngine;

// ============================================================================
// ZONE TRACKER - Tracks player time spent in specific level zones
// ============================================================================
// 
// This script attaches to trigger zones in the level and tracks how long the
// player spends in each zone. It integrates with ZoneManager for persistent
// statistics across the entire level.
//
// FEATURES:
// 1. Tracks time spent in zone (real-time)
// 2. Integrates with ZoneManager for persistent stats
// 3. Visual debugging with gizmos
// 4. Zone name and number for identification
// 5. Runtime queries for current zone status
//
// USE CASES:
// - Performance analysis (which areas take players longest)
// - Speedrun statistics (time per section)
// - Difficulty balancing (identify problematic areas)
// - Player behavior analysis (where they spend most time)
//
// INTEGRATION:
// - OnTriggerEnter: Notifies ZoneManager when player enters
// - OnTriggerExit: Sends time spent to ZoneManager
// - OnTriggerStay: Accumulates time while inside
//
// ============================================================================

public class ZoneTracker : MonoBehaviour
{
    // ========================================================================
    // SERIALIZED FIELDS - ZONE SETTINGS
    // ========================================================================

    [Header("=== ZONE SETTINGS ===")]
    [Tooltip("Display name of the zone (e.g., 'Start Area', 'Hut Area')")]
    [SerializeField] private string zoneName = "Zone 1";

    [Tooltip("Unique zone number (1, 2, 3, etc.) for identification")]
    [SerializeField] private int zoneNumber = 1;

    [Tooltip("Color of the gizmo visualization in Scene view")]
    [SerializeField] private Color gizmoColor = new Color(0f, 1f, 0f, 0.2f);

    [Header("=== TRACKING ===")]
    [Tooltip("Tag used to identify the player")]
    [SerializeField] private string playerTag = "Player";

    // ========================================================================
    // PRIVATE FIELDS - RUNTIME TRACKING
    // ========================================================================

    private float timeInZone = 0f;      // Current session time in this zone
    private bool playerInZone = false;  // Is player currently in this zone?
    private GameObject player;          // Reference to player GameObject

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    private void Start()
    {
        // Find player reference if not already set
        if (player == null)
            player = GameObject.FindGameObjectWithTag(playerTag);

        // Reset timer on start
        timeInZone = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the entering object is the player
        if (other.CompareTag(playerTag))
        {
            playerInZone = true;
            player = other.gameObject;
            timeInZone = 0f;  // Reset timer for new visit

            //Debug.Log($"Player entered {zoneName} (Zone {zoneNumber})");

            // Notify ZoneManager for persistent statistics
            if (ZoneManager.Instance != null)
                ZoneManager.Instance.OnPlayerEnterZone(zoneNumber, zoneName);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // Accumulate time while player stays in zone
        if (!playerInZone || !other.CompareTag(playerTag)) return;

        timeInZone += Time.deltaTime;
    }

    private void OnTriggerExit(Collider other)
    {
        // Check if the exiting object is the player
        if (other.CompareTag(playerTag) && playerInZone)
        {
            playerInZone = false;

            //Debug.Log($"ZONE {zoneNumber} TIME: {timeInZone:F1} seconds");

            // Notify ZoneManager with time spent
            if (ZoneManager.Instance != null)
                ZoneManager.Instance.OnPlayerExitZone(zoneNumber, zoneName, timeInZone);
        }
    }

    // ========================================================================
    // PUBLIC METHODS - QUERIES
    // ========================================================================

    /// <summary>
    /// Gets the total time spent in this zone during the current visit.
    /// </summary>
    public float GetTimeInZone() => timeInZone;

    /// <summary>
    /// Checks if the player is currently inside this zone.
    /// </summary>
    public bool IsPlayerInZone() => playerInZone;

    // ========================================================================
    // DEBUG VISUALIZATION
    // ========================================================================

    /// <summary>
    /// Draws the zone bounds in the Scene view for debugging.
    /// Shows zone name and current time when in Play mode.
    /// </summary>
    private void OnDrawGizmos()
    {
        // Set gizmo color from inspector
        Gizmos.color = gizmoColor;

        // Draw wireframe cube representing zone bounds
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);

            // Draw text label in Scene view
#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.white;
            string label = $"{zoneName}\n";

            // Show current time when in Play mode
            if (Application.isPlaying)
                label += $"{timeInZone:F1}s";
            else
                label += "0.0s";

            UnityEditor.Handles.Label(col.bounds.center + Vector3.up * 2, label);
#endif
        }
    }
}