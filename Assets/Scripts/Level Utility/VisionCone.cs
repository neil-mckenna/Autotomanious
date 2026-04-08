using UnityEngine;

// ============================================================================
// VISION CONE - DYNAMIC VISUAL REPRESENTATION OF GUARD VISION
// ============================================================================
// 
// This script creates a visual cone that shows the guard's field of view.
// It features:
// 1. Dynamic mesh generation that shows obstacles blocking vision
// 2. Color coding based on guard state (idle, chasing, suspicious)
// 3. Real-time updating as guard rotates and moves
// 4. Obstacle raycasting to show "shadowed" areas behind walls
//
// HOW IT WORKS:
// - The cone is a mesh that fans out from the guard's position
// - Rays are cast at regular angles within the field of view
// - If a ray hits an obstacle, the cone is truncated at that distance
// - This creates a realistic "shadow" effect behind obstacles
//
// COLOR LEGEND:
// - Orange: Idle/Patrolling (default state)
// - Red: Chasing (has line of sight to player)
// - Yellow: Suspicious (investigating a noise)
// - Gray: Blocked (vision partially blocked)
//
// ============================================================================

public class VisionCone : MonoBehaviour
{
    // ========================================================================
    // SERIALIZED FIELDS - CONFIGURE IN UNITY INSPECTOR
    // ========================================================================

    [Header("=== POSITION SETTINGS ===")]
    [Tooltip("Height of the cone above the guard (meters)")]
    [SerializeField] private float heightOffset = 1.5f;

    [Tooltip("Scale multiplier for simple cone mode (not used in dynamic mode)")]
    [SerializeField] private float distanceScale = 0.3f;

    [Header("=== OBSTACLE DETECTION ===")]
    [Tooltip("Layers that block vision (walls, obstacles, etc.)")]
    [SerializeField] private LayerMask obstacleLayerMask = -1;

    [Tooltip("Enable dynamic obstacle blocking (shows truncated cone behind walls)")]
    [SerializeField] private bool enableObstacleBlocking = true;

    [Tooltip("Number of raycast segments (higher = smoother cone edges, more performance cost)")]
    [SerializeField] private int raycastSegments = 36;

    [Header("=== COLOR SETTINGS ===")]
    [Tooltip("Color when guard is idle or patrolling")]
    [SerializeField] private Color idleColor = new Color(1f, 0.5f, 0f, 0.2f);

    [Tooltip("Color when guard is chasing the player")]
    [SerializeField] private Color chasingColor = new Color(1f, 0f, 0f, 0.4f);

    [Tooltip("Color when vision is blocked by obstacles")]
    [SerializeField] private Color blockedColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);

    [Tooltip("Color when guard is suspicious (investigating noise)")]
    [SerializeField] private Color suspiciousColor = new Color(1f, 1f, 0f, 0.3f);

    [Header("=== DEBUG ===")]
    [Tooltip("Show debug raycasts in Scene view")]
    [SerializeField] private bool showDebug = false;

    // ========================================================================
    // PRIVATE REFERENCES
    // ========================================================================

    private Guard guard;                 // Reference to the guard component
    private SpriteRenderer spriteRenderer; // Original sprite renderer (simple mode)
    private Transform coneVisual;        // Child transform containing the visual
    private Material coneMaterial;       // Material for dynamic cone mode
    private FSM fsm;                     // Reference to FSM brain (if using FSM)
    private MeshFilter coneMeshFilter;   // Mesh filter for dynamic cone

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    private void Start()
    {
        // Get references to guard and its AI brain
        guard = GetComponentInParent<Guard>();
        fsm = guard?.currentBrain as FSM;

        // Find the cone visual child object
        if (transform.childCount > 0)
        {
            coneVisual = transform.GetChild(0);
            spriteRenderer = coneVisual.GetComponent<SpriteRenderer>();

            // If using obstacle blocking, switch from sprite to dynamic mesh
            if (spriteRenderer != null && enableObstacleBlocking)
            {
                CreateDynamicConeMesh();
            }
        }

        // Validate sprite renderer exists
        if (spriteRenderer == null && coneMeshFilter == null)
        {
            Debug.LogError("VisionCone: No visual component found on child!");
            return;
        }

        // Set initial color
        if (spriteRenderer != null)
            spriteRenderer.color = idleColor;

        Debug.Log("VisionCone initialized");
    }

    private void Update()
    {
        // Choose update method based on mode
        if (!enableObstacleBlocking || coneMeshFilter == null)
        {
            UpdateSimpleCone();      // Simple scaling (no obstacle detection)
        }
        else
        {
            UpdateDynamicCone();     // Dynamic mesh with obstacle detection
        }
    }

    // ========================================================================
    // INITIALIZATION METHODS
    // ========================================================================

    /// <summary>
    /// Creates a dynamic mesh system for obstacle-aware vision cone.
    /// Replaces the simple sprite renderer with mesh renderer for dynamic shape.
    /// </summary>
    private void CreateDynamicConeMesh()
    {
        // Add or get MeshFilter
        coneMeshFilter = coneVisual.gameObject.GetComponent<MeshFilter>();
        if (coneMeshFilter == null)
        {
            coneMeshFilter = coneVisual.gameObject.AddComponent<MeshFilter>();
        }

        // Add or get MeshRenderer
        MeshRenderer meshRenderer = coneVisual.gameObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = coneVisual.gameObject.AddComponent<MeshRenderer>();
        }

        // Copy material from sprite renderer, then destroy it
        if (spriteRenderer != null)
        {
            meshRenderer.material = spriteRenderer.material;
            Destroy(spriteRenderer);
        }

        coneMaterial = meshRenderer.material;
    }

    // ========================================================================
    // UPDATE METHODS
    // ========================================================================

    /// <summary>
    /// Simple cone mode - just scales a sprite.
    /// Does NOT show obstacle blocking, but is more performant.
    /// </summary>
    private void UpdateSimpleCone()
    {
        if (spriteRenderer == null || guard == null) return;

        // Follow guard position and rotation
        transform.position = guard.transform.position + Vector3.up * heightOffset;
        transform.rotation = guard.transform.rotation;

        // Scale cone to match detection range
        float range = guard.GetDetectionRange();
        coneVisual.localScale = Vector3.one * (range * distanceScale);

        // Update color based on guard state
        UpdateConeColor();
    }

    /// <summary>
    /// Dynamic cone mode - generates a mesh that shows obstacles blocking vision.
    /// Raycasts at regular angles to determine how far vision extends.
    /// </summary>
    private void UpdateDynamicCone()
    {
        if (guard == null) return;

        // Follow guard position and rotation
        transform.position = guard.transform.position + Vector3.up * heightOffset;
        transform.rotation = guard.transform.rotation;

        // Build mesh based on raycast results
        float detectionRange = guard.GetDetectionRange();
        float halfFOV = guard.GetFieldOfView() / 2f;

        Mesh mesh = BuildBlockedConeMesh(detectionRange, halfFOV);
        coneMeshFilter.mesh = mesh;

        // Update color based on guard state
        UpdateConeColor();
    }

    // ========================================================================
    // MESH GENERATION
    // ========================================================================

    /// <summary>
    /// Builds a dynamic mesh that represents the guard's field of view,
    /// truncated by obstacles like walls.
    /// </summary>
    /// <param name="maxRange">Maximum detection range (meters)</param>
    /// <param name="halfFOV">Half of the field of view angle (degrees)</param>
    /// <returns>Generated mesh showing visible area</returns>
    private Mesh BuildBlockedConeMesh(float maxRange, float halfFOV)
    {
        Mesh mesh = new Mesh();

        Vector3 eyePosition = transform.position;
        Vector3 forward = transform.forward;

        // Calculate raycast angles
        int segments = raycastSegments;
        float angleStep = guard.GetFieldOfView() / segments;
        float startAngle = -halfFOV;

        // Vertex array: origin + one vertex per raycast segment
        Vector3[] vertices = new Vector3[segments + 2];
        Vector2[] uv = new Vector2[segments + 2];
        int[] triangles = new int[segments * 3];

        // First vertex is the origin (tip of the cone)
        vertices[0] = Vector3.zero;
        uv[0] = new Vector2(0.5f, 0);

        // Raycast to find hit distances for each angle
        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = startAngle + (i * angleStep);
            Vector3 direction = Quaternion.Euler(0, currentAngle, 0) * forward;

            float hitDistance = maxRange;
            RaycastHit hit;

            // Cast ray to detect obstacles
            if (Physics.Raycast(eyePosition, direction, out hit, maxRange, obstacleLayerMask))
            {
                // Convert hit point to local space
                Vector3 hitPoint = hit.point - eyePosition;
                hitDistance = hitPoint.magnitude;

                if (showDebug)
                {
                    Debug.DrawLine(eyePosition, hit.point, Color.red, 0.1f);
                }
            }
            else
            {
                if (showDebug)
                {
                    Debug.DrawRay(eyePosition, direction * maxRange, Color.green, 0.1f);
                }
            }

            // Calculate vertex position in local space
            Vector3 vertexPos = direction * hitDistance;
            vertices[i + 1] = vertexPos;
            uv[i + 1] = new Vector2((float)i / segments, 1);
        }

        // Build triangles (fan shape from origin to each edge segment)
        for (int i = 0; i < segments; i++)
        {
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;           // Origin
            triangles[triangleIndex + 1] = i + 1;    // Current edge vertex
            triangles[triangleIndex + 2] = i + 2;    // Next edge vertex
        }

        // Apply mesh data
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        return mesh;
    }

    // ========================================================================
    // COLOR MANAGEMENT
    // ========================================================================

    /// <summary>
    /// Updates the cone color based on guard's current state.
    /// Colors:
    /// - Red: Guard can see player (chasing)
    /// - Yellow: Guard is suspicious (investigating noise)
    /// - Orange: Guard is idle (patrolling)
    /// </summary>
    private void UpdateConeColor()
    {
        if (spriteRenderer == null && coneMaterial == null) return;

        bool canSeePlayer = guard.CanSeePlayer;
        string currentState = GetGuardState();

        Color targetColor;

        if (canSeePlayer)
        {
            targetColor = chasingColor;      // Red - ACTIVE CHASE
        }
        else if (currentState == "Suspicious")
        {
            targetColor = suspiciousColor;   // Yellow - SUSPICIOUS
        }
        else
        {
            targetColor = idleColor;         // Orange - IDLE/PATROL
        }

        // Apply color to appropriate renderer
        if (spriteRenderer != null)
        {
            spriteRenderer.color = targetColor;
        }
        else if (coneMaterial != null)
        {
            coneMaterial.color = targetColor;
        }
    }

    /// <summary>
    /// Gets the current state of the guard's AI brain.
    /// Supports FSM and can be extended for other brain types.
    /// </summary>
    private string GetGuardState()
    {
        if (fsm != null)
            return fsm.GetCurrentState();
        return "Unknown";
    }

    // ========================================================================
    // DEBUG VISUALIZATION
    // ========================================================================

    private void OnDrawGizmosSelected()
    {
        if (!enableObstacleBlocking || guard == null) return;

        // Draw a small marker at the cone origin when selected
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
}