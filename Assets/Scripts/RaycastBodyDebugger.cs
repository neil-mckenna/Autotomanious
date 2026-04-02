using UnityEngine;
using System.Collections.Generic;

public class RaycastBodyDebugger : MonoBehaviour
{
    [Header("Body Target Heights")]
    public float headHeight = 1.7f;
    public float chestHeight = 1.3f;
    public float waistHeight = 1.0f;
    public float hipsHeight = 0.7f;
    public float kneesHeight = 0.4f;

    [Header("Visual Settings")]
    public float markerSize = 0.25f;
    public float lineDuration = 0.5f;
    public bool showHitMarkers = true;
    public bool showObjectNames = true;

    [Header("Colors")]
    public Color headColor = new Color(1f, 0f, 1f, 1f);     // Magenta
    public Color chestColor = new Color(0f, 1f, 0f, 1f);     // Green
    public Color waistColor = new Color(1f, 1f, 0f, 1f);     // Yellow
    public Color hipsColor = new Color(0f, 1f, 1f, 1f);      // Cyan
    public Color kneesColor = new Color(1f, 0.5f, 0f, 1f);   // Orange
    public Color hitColor = Color.green;
    public Color blockedColor = Color.red;
    public Color missColor = Color.gray;

    private List<HitData> activeHits = new List<HitData>();

    private class HitData
    {
        public Vector3 position;
        public string bodyPart;
        public string objectName;
        public float distance;
        public float time;
        public Color color;
        public bool isHit;
    }

    void Update()
    {
        // Remove old markers
        activeHits.RemoveAll(h => Time.time - h.time > lineDuration);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        foreach (HitData hit in activeHits)
        {
            if (!showHitMarkers) continue;

            // Draw cross marker
            Gizmos.color = hit.color;
            float size = markerSize;

            // Horizontal line
            Gizmos.DrawLine(hit.position + Vector3.left * size, hit.position + Vector3.right * size);
            // Vertical line
            Gizmos.DrawLine(hit.position + Vector3.down * size, hit.position + Vector3.up * size);
            // Depth line
            Gizmos.DrawLine(hit.position + Vector3.back * size, hit.position + Vector3.forward * size);

            // Draw circle around hit
            DrawCircle(hit.position, size * 0.6f, hit.color);

#if UNITY_EDITOR
            if (showObjectNames)
            {
                string label = $"{hit.bodyPart}\n{hit.objectName}\n{hit.distance:F1}m";
                UnityEditor.Handles.color = hit.color;
                UnityEditor.Handles.Label(hit.position + Vector3.up * 0.3f, label);
            }
#endif
        }
    }

    private void DrawCircle(Vector3 center, float radius, Color color)
    {
        int segments = 24;
        float angleStep = 360f / segments;

        Vector3 prevPoint = center + Quaternion.Euler(0, 0, 0) * Vector3.right * radius;
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep;
            Vector3 newPoint = center + Quaternion.Euler(0, angle, 0) * Vector3.right * radius;
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }

    public void RecordHit(Vector3 position, string bodyPart, string objectName, float distance, bool isPlayer)
    {
        HitData hit = new HitData();
        hit.position = position;
        hit.bodyPart = bodyPart;
        hit.objectName = objectName;
        hit.distance = distance;
        hit.time = Time.time;
        hit.isHit = isPlayer;
        hit.color = isPlayer ? hitColor : blockedColor;

        activeHits.Add(hit);

        // Console log
        if (isPlayer)
        {
            Debug.Log($" HIT PLAYER! Body: {bodyPart}, Distance: {distance:F2}m at {position}");
        }
        else
        {
            Debug.Log($" HIT: {objectName} ({bodyPart}) at {distance:F2}m - Position: {position}");
        }
    }

    public void RecordMiss(Vector3 position, string bodyPart, float distance)
    {
        HitData hit = new HitData();
        hit.position = position;
        hit.bodyPart = bodyPart;
        hit.objectName = "MISS";
        hit.distance = distance;
        hit.time = Time.time;
        hit.isHit = false;
        hit.color = missColor;

        activeHits.Add(hit);
        Debug.Log($" MISS: {bodyPart} - Nothing hit at {distance:F2}m");
    }
}