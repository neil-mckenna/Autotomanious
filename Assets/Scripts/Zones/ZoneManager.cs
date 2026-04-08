using UnityEngine;
using System.Collections.Generic;
using System.Text;

// ============================================================================
// ZONE MANAGER - Tracks player time spent in different level zones
// ============================================================================
// 
// This singleton manager tracks how much time the player spends in each
// designated zone throughout the level. It persists across scene loads.
//
// FEATURES:
// 1. Tracks total time spent per zone
// 2. Records best time (fastest completion) per zone
// 3. Counts number of times player entered each zone
// 4. Provides formatted statistics for UI display
// 5. Singleton pattern for global access
//
// INTEGRATION:
// - ZoneTrigger scripts call OnPlayerEnterZone and OnPlayerExitZone
// - GameManager queries GetZoneStats() for victory/game over screens
// - Automatically persists across scene loads with DontDestroyOnLoad
//
// ============================================================================

public class ZoneManager : MonoBehaviour
{
    // ========================================================================
    // SINGLETON
    // ========================================================================

    public static ZoneManager Instance { get; private set; }

    // ========================================================================
    // PUBLIC CLASSES
    // ========================================================================

    /// <summary>
    /// Data structure for storing zone statistics.
    /// Each zone in the level has its own instance.
    /// </summary>
    [System.Serializable]
    public class ZoneData
    {
        [Tooltip("Unique identifier for the zone (1, 2, 3, etc.)")]
        public int zoneNumber;

        [Tooltip("Display name of the zone (Start Area, Hut Area, etc.)")]
        public string zoneName;

        [Tooltip("Total time spent in this zone (accumulated across visits)")]
        public float timeSpent;

        [Tooltip("Fastest time ever recorded for this zone")]
        public float bestTime;

        [Tooltip("Number of times player entered this zone")]
        public int timesEntered;

        [Tooltip("Whether to show this zone in UI panels")]
        public bool showInUI = true;
    }

    // ========================================================================
    // SERIALIZED FIELDS - CONFIGURE IN UNITY INSPECTOR
    // ========================================================================

    [Header("=== ZONE STATISTICS ===")]
    [Tooltip("List of zones being tracked (auto-populated during gameplay)")]
    public List<ZoneData> zoneStats = new List<ZoneData>();

    [Header("=== SETTINGS ===")]
    [Tooltip("Enable detailed debug logging")]
    public bool enableLogging = true;

    [Tooltip("Automatically reset stats when a new scene loads")]
    public bool autoResetOnSceneLoad = true;

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    private void Awake()
    {
        // Singleton setup - ensures only one instance exists
        if (Instance == null)
        {
            Instance = this;

            // Detach from parent to ensure it's a root object
            // This prevents issues with DontDestroyOnLoad
            if (transform.parent != null)
            {
                if (enableLogging)
                    Debug.Log("ZoneManager had a parent. Detaching to root.");
                transform.SetParent(null);
            }

            // Persist across scene loads
            DontDestroyOnLoad(gameObject);

            if (enableLogging)
                Debug.Log("ZoneManager initialized - will persist across scenes");
        }
        else if (Instance != this)
        {
            // Destroy duplicate instances
            Debug.Log("ZoneManager duplicate destroyed");
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Reset stats on new level start if configured
        if (autoResetOnSceneLoad)
            ResetStats();
    }

    // ========================================================================
    // PUBLIC METHODS - CALLED BY ZONE TRIGGERS
    // ========================================================================

    /// <summary>
    /// Called when player enters a zone.
    /// Increments entry counter for this zone.
    /// </summary>
    /// <param name="zoneNumber">Unique zone identifier</param>
    /// <param name="zoneName">Display name of the zone</param>
    public void OnPlayerEnterZone(int zoneNumber, string zoneName)
    {
        ZoneData zone = GetOrCreateZone(zoneNumber, zoneName);
        zone.timesEntered++;

        if (enableLogging)
            Debug.Log($"ZONE {zoneNumber}: Player entered (Visit #{zone.timesEntered})");
    }

    /// <summary>
    /// Called when player exits a zone.
    /// Adds time spent to total and checks for best time record.
    /// </summary>
    /// <param name="zoneNumber">Unique zone identifier</param>
    /// <param name="zoneName">Display name of the zone</param>
    /// <param name="timeSpent">Time spent in this zone (seconds)</param>
    public void OnPlayerExitZone(int zoneNumber, string zoneName, float timeSpent)
    {
        ZoneData zone = GetOrCreateZone(zoneNumber, zoneName);
        zone.timeSpent += timeSpent;

        // Check if this is a new best time
        if (zone.bestTime == 0 || timeSpent < zone.bestTime)
        {
            zone.bestTime = timeSpent;
            if (enableLogging)
                Debug.Log($"NEW BEST TIME in {zoneName}: {FormatTime(timeSpent)}");
        }

        if (enableLogging)
            Debug.Log($"ZONE {zoneNumber}: Spent {FormatTime(timeSpent)} (Total: {FormatTime(zone.timeSpent)})");
    }

    // ========================================================================
    // PRIVATE HELPER METHODS
    // ========================================================================

    /// <summary>
    /// Gets existing zone data or creates a new entry if not found.
    /// </summary>
    private ZoneData GetOrCreateZone(int zoneNumber, string zoneName)
    {
        // Try to find existing zone
        foreach (var zone in zoneStats)
        {
            if (zone.zoneNumber == zoneNumber)
                return zone;
        }

        // Create new zone entry
        ZoneData newZone = new ZoneData
        {
            zoneNumber = zoneNumber,
            zoneName = zoneName,
            timeSpent = 0f,
            bestTime = 0f,
            timesEntered = 0,
            showInUI = true
        };
        zoneStats.Add(newZone);
        return newZone;
    }

    /// <summary>
    /// Formats time in seconds to MM:SS format.
    /// </summary>
    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60);
        return $"{minutes:00}:{seconds:00}";
    }

    // ========================================================================
    // PUBLIC GETTER METHODS - FOR GAMEMANAGER AND UI
    // ========================================================================

    /// <summary>
    /// Returns all zone statistics.
    /// Used by GameManager to display stats in victory/game over panels.
    /// </summary>
    public List<ZoneData> GetZoneStats()
    {
        return zoneStats;
    }

    /// <summary>
    /// Gets data for a specific zone by number.
    /// Returns null if zone not found.
    /// </summary>
    public ZoneData GetZone(int zoneNumber)
    {
        foreach (var zone in zoneStats)
        {
            if (zone.zoneNumber == zoneNumber)
                return zone;
        }
        return null;
    }

    /// <summary>
    /// Gets total time spent in a specific zone (seconds).
    /// </summary>
    public float GetZoneTime(int zoneNumber)
    {
        foreach (var zone in zoneStats)
        {
            if (zone.zoneNumber == zoneNumber)
                return zone.timeSpent;
        }
        return 0f;
    }

    /// <summary>
    /// Gets formatted time string for a specific zone.
    /// </summary>
    public string GetZoneTimeFormatted(int zoneNumber)
    {
        return FormatTime(GetZoneTime(zoneNumber));
    }

    // ========================================================================
    // STATISTICS REPORTING
    // ========================================================================

    /// <summary>
    /// Generates a formatted string containing all zone statistics.
    /// Useful for debugging and end-of-level reports.
    /// </summary>
    public string GetFormattedStats()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=======================================");
        sb.AppendLine("       ZONE STATISTICS");
        sb.AppendLine("=======================================");

        float totalTime = 0f;
        foreach (var zone in zoneStats)
        {
            sb.AppendLine($"Zone {zone.zoneNumber}: {zone.zoneName}");
            sb.AppendLine($"  Total time: {zone.timeSpent:F2}s");
            sb.AppendLine($"  Best time: {FormatTime(zone.bestTime)}");
            sb.AppendLine($"  Times entered: {zone.timesEntered}");
            sb.AppendLine("");
            totalTime += zone.timeSpent;
        }

        sb.AppendLine("=======================================");
        sb.AppendLine($"TOTAL TIME: {FormatTime(totalTime)}");
        sb.AppendLine("=======================================");

        return sb.ToString();
    }

    /// <summary>
    /// Prints zone statistics to Unity console.
    /// </summary>
    public void PrintStats()
    {
        Debug.Log(GetFormattedStats());
    }

    // ========================================================================
    // STATISTICS MANAGEMENT
    // ========================================================================

    /// <summary>
    /// Resets all zone statistics.
    /// Called when starting a new game or reloading the level.
    /// </summary>
    public void ResetStats()
    {
        zoneStats.Clear();

        if (enableLogging)
            Debug.Log("Zone stats reset");
    }

    // ========================================================================
    // CONTEXT MENU METHODS - FOR EDITOR USE
    // ========================================================================

    [ContextMenu("Print Zone Stats")]
    private void ContextPrintStats()
    {
        PrintStats();
    }

    [ContextMenu("Reset Zone Stats")]
    private void ContextResetStats()
    {
        ResetStats();
    }
}