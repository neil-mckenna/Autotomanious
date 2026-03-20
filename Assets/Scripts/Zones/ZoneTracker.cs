using UnityEngine;

public class ZoneTracker : MonoBehaviour
{
    [Header("Zone Settings")]
    [SerializeField] private string zoneName = "Zone 1";
    [SerializeField] private int zoneNumber = 1;
    [SerializeField] private Color gizmoColor = new Color(0f, 1f, 0f, 0.2f);

    [Header("Tracking")]
    [SerializeField] private string playerTag = "Player";

    // Runtime tracking
    private float timeInZone = 0f;
    private bool playerInZone = false;
    private GameObject player;

    private void Start()
    {
        // Find player
        if (player == null)
            player = GameObject.FindGameObjectWithTag(playerTag);

        timeInZone = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInZone = true;
            player = other.gameObject;
            timeInZone = 0f;

            Debug.Log($" Player entered {zoneName} (Zone {zoneNumber})");

            // Notify ZoneManager
            if (ZoneManager.Instance != null)
                ZoneManager.Instance.OnPlayerEnterZone(zoneNumber, zoneName);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!playerInZone || !other.CompareTag(playerTag)) return;

        timeInZone += Time.deltaTime;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag) && playerInZone)
        {
            playerInZone = false;

            Debug.Log($" ZONE {zoneNumber} TIME: {timeInZone:F1} seconds");

            // Notify ZoneManager
            if (ZoneManager.Instance != null)
                ZoneManager.Instance.OnPlayerExitZone(zoneNumber, zoneName, timeInZone);
        }
    }

    public float GetTimeInZone() => timeInZone;
    public bool IsPlayerInZone() => playerInZone;

    private void OnDrawGizmos()
    {
        // Visualize zone in editor
        Gizmos.color = gizmoColor;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);

#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(col.bounds.center + Vector3.up * 2,
                $"{zoneName}\n{(Application.isPlaying ? timeInZone.ToString("F1") : "0.0")}s");
#endif
        }
    }
}
