using UnityEngine;
using System.Collections.Generic;
using System.Text;

public class ZoneManager : MonoBehaviour
{
    public static ZoneManager Instance { get; private set; }

    [System.Serializable]
    public class ZoneData
    {
        public int zoneNumber;
        public string zoneName;
        public float timeSpent;
        public float bestTime;
        public int timesEntered;
        public bool showInUI = true;
    }

    [Header("Zone Statistics")]
    public List<ZoneData> zoneStats = new List<ZoneData>();

    [Header("Settings")]
    public bool enableLogging = true;
    public bool autoResetOnSceneLoad = true;

    private void Awake()
    {
        // Singleton setup 
        if (Instance == null)
        {
            Instance = this;

            // Ensure it's a root object
            if (transform.parent != null)
            {
                Debug.LogWarning("ZoneManager had a parent. Detaching to root.");
                transform.SetParent(null);
            }

            // Now it's safe to call DontDestroyOnLoad
            DontDestroyOnLoad(gameObject);
            Debug.Log("ZoneManager initialized - will persist across scenes");
        }
        else if (Instance != this)
        {
            Debug.Log("ZoneManager duplicate destroyed");
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (autoResetOnSceneLoad)
            ResetStats();
    }

    public void OnPlayerEnterZone(int zoneNumber, string zoneName)
    {
        ZoneData zone = GetOrCreateZone(zoneNumber, zoneName);
        zone.timesEntered++;

        if (enableLogging)
            Debug.Log($"ZONE {zoneNumber}: Player entered (Visit #{zone.timesEntered})");
    }

    public void OnPlayerExitZone(int zoneNumber, string zoneName, float timeSpent)
    {
        ZoneData zone = GetOrCreateZone(zoneNumber, zoneName);
        zone.timeSpent += timeSpent;

        if (zone.bestTime == 0 || timeSpent < zone.bestTime)
        {
            zone.bestTime = timeSpent;
            if (enableLogging)
                Debug.Log($"NEW BEST TIME in {zoneName}: {FormatTime(timeSpent)}");
        }

        if (enableLogging)
            Debug.Log($"ZONE {zoneNumber}: Spent {FormatTime(timeSpent)} (Total: {FormatTime(zone.timeSpent)})");
    }

    private ZoneData GetOrCreateZone(int zoneNumber, string zoneName)
    {
        foreach (var zone in zoneStats)
        {
            if (zone.zoneNumber == zoneNumber)
                return zone;
        }

        ZoneData newZone = new ZoneData
        {
            zoneNumber = zoneNumber,
            zoneName = zoneName,
            timeSpent = 0f,
            bestTime = 0f,
            timesEntered = 0
        };
        zoneStats.Add(newZone);
        return newZone;
    }

    // GET ZONE STATS METHODS - Use these in GameManager!

    public List<ZoneData> GetZoneStats()
    {
        return zoneStats;
    }

    public ZoneData GetZone(int zoneNumber)
    {
        foreach (var zone in zoneStats)
        {
            if (zone.zoneNumber == zoneNumber)
                return zone;
        }
        return null;
    }

    public float GetZoneTime(int zoneNumber)
    {
        foreach (var zone in zoneStats)
        {
            if (zone.zoneNumber == zoneNumber)
                return zone.timeSpent;
        }
        return 0f;
    }

    public string GetZoneTimeFormatted(int zoneNumber)
    {
        return FormatTime(GetZoneTime(zoneNumber));
    }

    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60);
        return $"{minutes:00}:{seconds:00}";
    }

    public string GetFormattedStats()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("???????????????????????????????????");
        sb.AppendLine("       ZONE STATISTICS");
        sb.AppendLine("???????????????????????????????????");

        float totalTime = 0f;
        foreach (var zone in zoneStats)
        {
            sb.AppendLine($"Zone {zone.zoneNumber}: {zone.zoneName}");
            sb.AppendLine($"  ?? Total time: {zone.timeSpent:F2}s");
            sb.AppendLine($"  ?? Best time: {FormatTime(zone.bestTime):F2}");
            sb.AppendLine($"  ?? Times entered: {zone.timesEntered}");
            totalTime += zone.timeSpent;
        }

        sb.AppendLine("???????????????????????????????????");
        sb.AppendLine($"TOTAL TIME: {FormatTime(totalTime):F2}");
        sb.AppendLine("???????????????????????????????????");

        return sb.ToString();
    }

    public void PrintStats()
    {
        Debug.Log(GetFormattedStats());
    }

    public void ResetStats()
    {
        zoneStats.Clear();
        Debug.Log("Zone stats reset");
    }

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