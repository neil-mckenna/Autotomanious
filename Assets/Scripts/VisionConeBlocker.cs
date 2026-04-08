using UnityEngine;
using System.Collections.Generic;

// ============================================================================
// VISION CONE BLOCKER - DYNAMIC VISION CONE WITH OBSTACLE DETECTION
// ============================================================================
// 
// This script creates a dynamic mesh-based vision cone that accurately shows
// what a guard can see, accounting for obstacles like walls and pillars.
// 
// KEY FEATURES:
// 1. Raycast-based mesh generation - shows exactly where vision is blocked
// 2. Ignores Player and Guard colliders (they don't block vision)
// 3. Color-coded by guard state (idle, chasing, suspicious)
// 4. Optimized updates (not every frame, uses update interval)
// 5. Gradient material for fade-out effect
//
// HOW IT WORKS:
// - Casts rays at regular angles within the guard's field of view
// - Each ray travels until it hits an obstacle or reaches max range
// - A mesh is created connecting all ray hit points
// - The result shows the exact visible area (green rays) vs blocked area (red rays)
//
// COMPARED TO VisionCone.cs:
// - This version uses MeshRenderer (not SpriteRenderer)
// - Has gradient fade effect (transparent at edges)
// - More optimized with update interval
// - Specifically excludes Player/Guard from blocking vision
//
// ============================================================================

public class VisionConeBlocker : MonoBehaviour
{
    // ========================================================================
    // SERIALIZED FIELDS - CONFIGURE IN UNITY INSPECTOR
    // ========================================================================

    [Header("=== POSITION & VISUAL SETTINGS ===")]
    [Tooltip("Height of the cone above the guard (meters)")]
    [SerializeField] private float heightOffset = 1.5f;

    [Header("=== OBSTACLE DETECTION ===")]
    [Tooltip("Layers that block vision (walls, obstacles, NOT player/guard)")]
    [SerializeField] private LayerMask obstacleLayerMask = -1;

    [Tooltip("Number of raycast segments (higher = smoother, more performance cost)")]
    [SerializeField] private int raycastSegments = 36;

    [Tooltip("How often to update the mesh (seconds) - lower = smoother but more CPU)")]
    [SerializeField] private float updateInterval = 0.05f;

    [Header("=== COLOR SETTINGS ===")]
    [Tooltip("Color when guard is idle or patrolling")]
    [SerializeField] private Color idleColor = new Color(1f, 0.5f, 0f, 0.3f);  // Orange

    [Tooltip("Color when guard is chasing the player")]
    [SerializeField] private Color chasingColor = new Color(1f, 0f, 0f, 0.5f); // Red

    [Tooltip("Color when guard is suspicious (investigating noise)")]
    [SerializeField] private Color suspiciousColor = new Color(1f, 1f, 0f, 0.4f); // Yellow

    [Header("=== MATERIAL REFERENCES ===")]
    [Tooltip("Material for the cone (auto-created if null)")]
    [SerializeField] private Material coneMaterial;

    // ========================================================================
    // PRIVATE REFERENCES
    // ========================================================================

    private Guard guard;           // Reference to the guard component
    private FSM fsm;               // Reference to FSM brain (for state detection)
    private MeshFilter meshFilter; // Mesh filter for dynamic mesh
    private MeshRenderer meshRenderer; // Mesh renderer for displaying cone
    private float lastUpdateTime;  // Last time mesh was updated

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    void Start()
    {
        // Get references to guard and its AI brain
        guard = GetComponentInParent<Guard>();
        fsm = guard?.currentBrain as FSM;

        // Setup mesh components for dynamic cone
        SetupMeshComponents();

        // Create default material if none assigned
        if (coneMaterial == null)
        {
            coneMaterial = CreateDefaultMaterial();
        }

        meshRenderer.material = coneMaterial;
    }

    void Update()
    {
        if (guard == null) return;

        // Follow guard position and rotation
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

    // ========================================================================
    // INITIALIZATION METHODS
    // ========================================================================

    /// <summary>
    /// Sets up MeshFilter and MeshRenderer components for dynamic cone.
    /// </summary>
    private void SetupMeshComponents()
    {
        // Add or get MeshFilter
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        // Add or get MeshRenderer
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        // Set render queue to transparent (render behind other transparent objects)
        meshRenderer.sortingOrder = -10;
    }

    /// <summary>
    /// Creates a default gradient material for the vision cone.
    /// The gradient fades from opaque at the guard to transparent at the edges.
    /// </summary>
    private Material CreateDefaultMaterial()
    {
        // Use transparent unlit shader
        Shader shader = Shader.Find("Unlit/Transparent");
        Material mat = new Material(shader);

        // Create gradient texture (fades from opaque to transparent)
        Texture2D texture = new Texture2D(1, 256);
        for (int i = 0; i < 256; i++)
        {
            float alpha = 1f - (i / 255f);  // Linear fade
            texture.SetPixel(0, i, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply();

        mat.mainTexture = texture;
        mat.SetColor("_Color", idleColor);

        return mat;
    }

    // ========================================================================
    // MESH GENERATION
    // ========================================================================

    /// <summary>
    /// Updates the vision cone mesh based on current guard position and obstacles.
    /// </summary>
    private void UpdateConeMesh()
    {
        float detectionRange = guard.GetDetectionRange();
        float halfFOV = guard.GetFieldOfView() / 2f;

        Mesh mesh = BuildBlockedConeMesh(detectionRange, halfFOV);
        meshFilter.mesh = mesh;
    }

    /// <summary>
    /// Builds a dynamic mesh representing the guard's field of view,
    /// truncated by obstacles like walls.
    /// 
    /// RAYCAST COLORS (for debugging):
    /// - Green ray: Reached max range (no obstacle)
    /// - Red ray: Hit an obstacle (vision blocked)
    /// </summary>
    /// <param name="maxRange">Maximum detection range (meters)</param>
    /// <param name="halfFOV">Half of the field of view angle (degrees)</param>
    /// <returns>Generated mesh showing visible area</returns>
    private Mesh BuildBlockedConeMesh(float maxRange, float halfFOV)
    {
        Mesh mesh = new Mesh();

        Vector3 eyePosition = transform.position;
        Vector3 forward = transform.forward;  // Already rotated with the guard

        int segments = raycastSegments;
        float angleStep = guard.GetFieldOfView() / segments;
        float startAngle = -halfFOV;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Origin vertex (tip of the cone at guard's position)
        vertices.Add(Vector3.zero);
        uvs.Add(new Vector2(0.5f, 0));

        // Raycast for each angle segment
        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = startAngle + (i * angleStep);
            // Rotate direction around guard's Y axis
            Vector3 worldDirection = Quaternion.Euler(0, currentAngle, 0) * forward;

            float hitDistance = maxRange;

            // Raycast in world space to detect obstacles
            RaycastHit hit;
            if (Physics.Raycast(eyePosition, worldDirection, out hit, maxRange, obstacleLayerMask))
            {
                // CRITICAL: Don't block on Player or Guard colliders
                // This ensures the cone shows vision THROUGH other characters
                if (hit.transform.GetComponent<Player>() == null &&
                    hit.transform.GetComponent<Guard>() == null)
                {
                    hitDistance = hit.distance;
                    Debug.DrawLine(eyePosition, hit.point, Color.red, 0.05f);   // Blocked by obstacle
                }
                else
                {
                    Debug.DrawLine(eyePosition, hit.point, Color.green, 0.05f); // Passing through character
                }
            }
            else
            {
                Debug.DrawRay(eyePosition, worldDirection * maxRange, Color.green, 0.05f); // No obstacle
            }

            // Convert world hit point to LOCAL space relative to this transform
            Vector3 worldHitPoint = eyePosition + (worldDirection * hitDistance);
            Vector3 localVertexPos = transform.InverseTransformPoint(worldHitPoint);

            vertices.Add(localVertexPos);
            uvs.Add(new Vector2((float)i / segments, 1));
        }

        // Build triangles (fan shape from origin to each edge segment)
        for (int i = 0; i < segments; i++)
        {
            triangles.Add(0);      // Origin vertex
            triangles.Add(i + 1);  // Current edge vertex
            triangles.Add(i + 2);  // Next edge vertex
        }

        // Apply mesh data
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        return mesh;
    }

    // ========================================================================
    // COLOR MANAGEMENT
    // ========================================================================

    /// <summary>
    /// Updates the cone color based on guard's current state.
    /// Colors match standard AI behavior indicators.
    /// </summary>
    private void UpdateConeColor()
    {
        if (coneMaterial == null) return;

        Color targetColor;

        if (guard.CanSeePlayer)
        {
            targetColor = chasingColor;      // Red - actively chasing
        }
        else if (fsm != null && fsm.GetCurrentState() == "Suspicious")
        {
            targetColor = suspiciousColor;   // Yellow - investigating
        }
        else
        {
            targetColor = idleColor;         // Orange - normal patrolling
        }

        coneMaterial.SetColor("_Color", targetColor);
    }
}