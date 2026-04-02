using UnityEngine;
using System.Collections.Generic;

public class VisionConeBlocker : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float heightOffset = 1.5f;
    [SerializeField] private LayerMask obstacleLayerMask = -1;
    [SerializeField] private int raycastSegments = 36;
    [SerializeField] private float updateInterval = 0.05f;

    [Header("Colors")]
    [SerializeField] private Color idleColor = new Color(1f, 0.5f, 0f, 0.3f);
    [SerializeField] private Color chasingColor = new Color(1f, 0f, 0f, 0.5f);
    [SerializeField] private Color suspiciousColor = new Color(1f, 1f, 0f, 0.4f);

    [Header("References")]
    [SerializeField] private Material coneMaterial;

    private Guard guard;
    private FSM fsm;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private float lastUpdateTime;

    void Start()
    {
        guard = GetComponentInParent<Guard>();
        fsm = guard?.currentBrain as FSM;

        // Setup mesh components
        SetupMeshComponents();

        // Create material if needed
        if (coneMaterial == null)
        {
            coneMaterial = CreateDefaultMaterial();
        }

        meshRenderer.material = coneMaterial;
    }

    void SetupMeshComponents()
    {
        // Add MeshFilter
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        // Add MeshRenderer
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        // Set render queue to transparent
        meshRenderer.sortingOrder = -10;
    }

    void Update()
    {
        if (guard == null) return;

        // Update position to follow guard
        transform.position = guard.transform.position + Vector3.up * heightOffset;
        transform.rotation = guard.transform.rotation;

        // Update mesh periodically (not every frame for performance)
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateConeMesh();
            lastUpdateTime = Time.time;
        }

        // Update color based on guard state
        UpdateConeColor();
    }

    void UpdateConeMesh()
    {
        float detectionRange = guard.GetDetectionRange();
        float halfFOV = guard.GetFieldOfView() / 2f;

        Mesh mesh = BuildBlockedConeMesh(detectionRange, halfFOV);
        meshFilter.mesh = mesh;
    }

    Mesh BuildBlockedConeMesh(float maxRange, float halfFOV)
    {
        Mesh mesh = new Mesh();

        Vector3 eyePosition = transform.position;
        Vector3 forward = transform.forward;

        int segments = raycastSegments;
        float angleStep = guard.GetFieldOfView() / segments;
        float startAngle = -halfFOV;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Origin vertex (0,0,0 in local space)
        vertices.Add(Vector3.zero);
        uvs.Add(new Vector2(0.5f, 0));

        // Raycast for each angle
        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = startAngle + (i * angleStep);
            Vector3 direction = Quaternion.Euler(0, currentAngle, 0) * forward;

            float hitDistance = maxRange;

            // Raycast to find where the cone gets blocked
            RaycastHit hit;
            if (Physics.Raycast(eyePosition, direction, out hit, maxRange, obstacleLayerMask))
            {
                // Don't block on Player or Guard
                if (hit.transform.GetComponent<Player>() == null &&
                    hit.transform.GetComponent<Guard>() == null)
                {
                    hitDistance = hit.distance;

                    // Visual debug (optional)
                    Debug.DrawLine(eyePosition, hit.point, Color.red, 0.05f);
                }
                else
                {
                    Debug.DrawLine(eyePosition, hit.point, Color.green, 0.05f);
                }
            }
            else
            {
                Debug.DrawRay(eyePosition, direction * maxRange, Color.green, 0.05f);
            }

            // Convert to local space
            Vector3 vertexPos = direction * hitDistance;
            vertices.Add(vertexPos);
            uvs.Add(new Vector2((float)i / segments, 1));
        }

        // Build triangles (fan shape)
        for (int i = 0; i < segments; i++)
        {
            triangles.Add(0);
            triangles.Add(i + 1);
            triangles.Add(i + 2);
        }

        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        return mesh;
    }

    void UpdateConeColor()
    {
        if (coneMaterial == null) return;

        Color targetColor;

        if (guard.CanSeePlayer)
            targetColor = chasingColor;
        else if (fsm != null && fsm.GetCurrentState() == "Suspicious")
            targetColor = suspiciousColor;
        else
            targetColor = idleColor;

        coneMaterial.SetColor("_Color", targetColor);
    }

    Material CreateDefaultMaterial()
    {
        // Create a simple transparent material
        Shader shader = Shader.Find("Unlit/Transparent");
        Material mat = new Material(shader);

        // Create a gradient texture
        Texture2D texture = new Texture2D(1, 256);
        for (int i = 0; i < 256; i++)
        {
            float alpha = 1f - (i / 255f);
            texture.SetPixel(0, i, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply();

        mat.mainTexture = texture;
        mat.SetColor("_Color", idleColor);

        return mat;
    }
}