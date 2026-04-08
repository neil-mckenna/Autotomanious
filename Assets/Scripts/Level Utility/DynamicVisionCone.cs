using UnityEngine;

// ============================================================================
// DYNAMIC VISION CONE - REAL-TIME MESH-BASED VISION CONE VISUALIZATION
// ============================================================================
// 
// This script creates a dynamic 3D mesh cone that visualizes the guard's field of view.
// It's more performant than raycast-based cones and provides real-time visual feedback.
//
// FEATURES:
// 1. Dynamic mesh generation that updates when range or FOV changes
// 2. Color-coded states (Idle, Searching, Chasing, Suspicious)
// 3. Smooth color transitions (lerp between colors)
// 4. Works with all AI brain types (Zombie, BTBrain, FSM)
// 5. Low performance cost (only regenerates mesh when parameters change)
// 6. PRESERVES ALPHA VALUES from editor/prefab settings
//
// COLOR LEGEND:
// - Light Blue: Idle/Patrolling (normal state)
// - Yellow: Searching/Suspicious (investigating)
// - Red: Chasing (actively pursuing player)
// - Purple: Suspicious (alert but no target)
//
// ============================================================================

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DynamicVisionCone : MonoBehaviour
{
    // ========================================================================
    // SERIALIZED FIELDS - REFERENCES
    // ========================================================================

    [Header("=== REFERENCES ===")]
    [Tooltip("Reference to the Guard component (auto-finds parent if null)")]
    [SerializeField] private Guard guard;

    // ========================================================================
    // SERIALIZED FIELDS - VISUAL SETTINGS
    // ========================================================================

    [Header("=== VISUAL SETTINGS ===")]
    [Tooltip("Material used for the vision cone (auto-created if null)")]
    [SerializeField] private Material coneMaterial;

    [Tooltip("Height of the cone above the guard (meters)")]
    [SerializeField] private float heightOffset = 0.5f;

    [Tooltip("Number of segments for mesh smoothness (higher = smoother)")]
    [SerializeField] private int segments = 24;

    // ========================================================================
    // SERIALIZED FIELDS - STATE COLORS
    // ========================================================================

    [Header("=== STATE COLORS ===")]
    [Tooltip("Color when guard is idle/patrolling (Light Blue)")]
    [SerializeField] private Color idleColor = new Color(0.5f, 0.8f, 1f, 0.1f);

    [Tooltip("Color when guard is searching/suspicious (Yellow)")]
    [SerializeField] private Color searchingColor = new Color(1f, 1f, 0f, 0.15f);

    [Tooltip("Color when guard is chasing the player (Red)")]
    [SerializeField] private Color chasingColor = new Color(1f, 0f, 0f, 0.2f);

    [Tooltip("Color when guard is suspicious (Purple)")]
    [SerializeField] private Color suspiciousColor = new Color(1f, 0f, 0.5f, 0.15f);

    // ========================================================================
    // PRIVATE FIELDS - COMPONENT REFERENCES
    // ========================================================================

    private MeshFilter meshFilter;      // Mesh filter for cone mesh
    private MeshRenderer meshRenderer;  // Mesh renderer for cone material
    private Mesh coneMesh;              // Generated cone mesh
    private float lastRange;            // Last detection range (for change detection)
    private float lastFOV;              // Last field of view (for change detection)

    // ========================================================================
    // PRIVATE FIELDS - STORED ALPHA VALUES
    // ========================================================================

    // Store the original alpha values from editor/prefab
    private float originalIdleAlpha;
    private float originalSearchingAlpha;
    private float originalChasingAlpha;
    private float originalSuspiciousAlpha;
    private bool hasStoredOriginalAlphas = false;

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    private void Start()
    {
        // Find guard reference if not assigned
        if (guard == null)
            guard = GetComponentInParent<Guard>();

        if (guard == null)
        {
            Debug.LogError("DynamicVisionCone: No Guard found! Component disabled.");
            enabled = false;
            return;
        }

        // Store original alpha values from editor/prefab settings
        StoreOriginalAlphaValues();

        // Get component references
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        // Create default material if none assigned
        if (coneMaterial == null)
        {
            coneMaterial = CreateDefaultMaterial();
        }
        meshRenderer.material = coneMaterial;

        // Apply stored alpha values to colors
        ApplyStoredAlphas();

        // Generate initial cone mesh
        GenerateConeMesh();

        // Store initial values for change detection
        lastRange = guard.GetDetectionRange();
        lastFOV = guard.GetFieldOfView();
    }

    private void Update()
    {
        if (guard == null) return;

        // Update position and rotation to follow guard
        transform.position = guard.transform.position + Vector3.up * heightOffset;
        transform.rotation = guard.transform.rotation;

        // Check if detection range or FOV changed
        float currentRange = guard.GetDetectionRange();
        float currentFOV = guard.GetFieldOfView();

        if (Mathf.Abs(currentRange - lastRange) > 0.01f ||
            Mathf.Abs(currentFOV - lastFOV) > 0.01f)
        {
            // Regenerate mesh when parameters change
            GenerateConeMesh();
            lastRange = currentRange;
            lastFOV = currentFOV;
        }

        // Update cone color based on guard state
        UpdateConeColor();
    }

    // ========================================================================
    // ALPHA PRESERVATION METHODS
    // ========================================================================

    /// <summary>
    /// Stores the original alpha values from the colors set in the editor/prefab.
    /// This ensures transparency settings are preserved throughout gameplay.
    /// </summary>
    private void StoreOriginalAlphaValues()
    {
        if (hasStoredOriginalAlphas) return;

        originalIdleAlpha = idleColor.a;
        originalSearchingAlpha = searchingColor.a;
        originalChasingAlpha = chasingColor.a;
        originalSuspiciousAlpha = suspiciousColor.a;
        hasStoredOriginalAlphas = true;

        Debug.Log($"DynamicVisionCone: Stored original alphas - Idle:{originalIdleAlpha}, Searching:{originalSearchingAlpha}, Chasing:{originalChasingAlpha}, Suspicious:{originalSuspiciousAlpha}");
    }

    /// <summary>
    /// Applies the stored alpha values to all colors.
    /// Called after color modifications to restore transparency.
    /// </summary>
    private void ApplyStoredAlphas()
    {
        idleColor = new Color(idleColor.r, idleColor.g, idleColor.b, originalIdleAlpha);
        searchingColor = new Color(searchingColor.r, searchingColor.g, searchingColor.b, originalSearchingAlpha);
        chasingColor = new Color(chasingColor.r, chasingColor.g, chasingColor.b, originalChasingAlpha);
        suspiciousColor = new Color(suspiciousColor.r, suspiciousColor.g, suspiciousColor.b, originalSuspiciousAlpha);
    }

    /// <summary>
    /// Called when values change in the Unity Inspector.
    /// Ensures alpha values are preserved when editing colors.
    /// </summary>
    private void OnValidate()
    {
        // Only apply if we have stored values (previons Start() has run)
        if (hasStoredOriginalAlphas)
        {
            ApplyStoredAlphas();
        }
    }

    // ========================================================================
    // MESH GENERATION
    // ========================================================================

    /// <summary>
    /// Generates a cone mesh based on the guard's detection range and FOV.
    /// Creates a fan-shaped mesh from the origin outward.
    /// </summary>
    private void GenerateConeMesh()
    {
        if (guard == null) return;

        float range = guard.GetDetectionRange();
        float fov = guard.GetFieldOfView() * Mathf.Deg2Rad;  // Convert to radians

        coneMesh = new Mesh();

        // Vertex array: origin + (segments + 1) edge points
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        // First vertex is the origin (tip of the cone)
        vertices[0] = Vector3.zero;

        // Calculate vertices along the cone arc
        float startAngle = -fov * 0.5f;
        float angleStep = fov / segments;

        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = startAngle + (i * angleStep);
            float x = Mathf.Sin(currentAngle) * range;
            float z = Mathf.Cos(currentAngle) * range;
            vertices[i + 1] = new Vector3(x, 0, z);
        }

        // Build triangles (fan shape from origin to each edge segment)
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;           // Origin
            triangles[i * 3 + 1] = i + 1;   // Current edge vertex
            triangles[i * 3 + 2] = i + 2;   // Next edge vertex
        }

        // Apply mesh data
        coneMesh.vertices = vertices;
        coneMesh.triangles = triangles;
        coneMesh.RecalculateNormals();

        meshFilter.mesh = coneMesh;
    }

    // ========================================================================
    // COLOR MANAGEMENT
    // ========================================================================

    /// <summary>
    /// Updates the cone color based on the guard's current state.
    /// Supports all AI brain types: Zombie, BTBrain, and FSM.
    /// Uses smooth color interpolation (lerp) for transitions.
    /// Preserves original alpha values from editor/prefab.
    /// </summary>
    private void UpdateConeColor()
    {
        Color targetColor = GetTargetColorForState();

        // Smoothly transition to target color while preserving alpha
        Color currentColor = meshRenderer.material.color;
        Color newColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * 5f);

        // Ensure alpha matches the target color's original alpha
        newColor.a = targetColor.a;

        meshRenderer.material.color = newColor;
    }

    /// <summary>
    /// Determines the target color based on the guard's current state.
    /// Returns the color with its original alpha from editor settings.
    /// </summary>
    private Color GetTargetColorForState()
    {
        // Check for Zombie brain
        if (guard.currentBrain is Zombie zombieBrain)
        {
            bool isChasing = zombieBrain.IsChasing();
            return isChasing ? chasingColor : idleColor;
        }
        // Check for Behavior Tree brain
        else if (guard.currentBrain is BTBrain btBrain)
        {
            string action = btBrain.GetCurrentAction();

            switch (action)
            {
                case "Chasing":
                    return chasingColor;
                case "Searching":
                    return searchingColor;
                case "Suspicious":
                    return suspiciousColor;
                default:
                    return idleColor;
            }
        }
        // Check for FSM brain
        else if (guard.currentBrain is FSM fsmBrain)
        {
            string state = fsmBrain.GetCurrentState();

            switch (state)
            {
                case "Chasing":
                    return chasingColor;
                case "Searching":
                    return searchingColor;
                case "Suspicious":
                    return suspiciousColor;
                default:
                    return idleColor;
            }
        }

        return idleColor;
    }

    // ========================================================================
    // PUBLIC METHODS - COLOR MODIFICATION
    // ========================================================================

    /// <summary>
    /// Sets the idle color while preserving its original alpha.
    /// </summary>
    public void SetIdleColor(Color newColor)
    {
        newColor.a = originalIdleAlpha;
        idleColor = newColor;
    }

    /// <summary>
    /// Sets the searching color while preserving its original alpha.
    /// </summary>
    public void SetSearchingColor(Color newColor)
    {
        newColor.a = originalSearchingAlpha;
        searchingColor = newColor;
    }

    /// <summary>
    /// Sets the chasing color while preserving its original alpha.
    /// </summary>
    public void SetChasingColor(Color newColor)
    {
        newColor.a = originalChasingAlpha;
        chasingColor = newColor;
    }

    /// <summary>
    /// Sets the suspicious color while preserving its original alpha.
    /// </summary>
    public void SetSuspiciousColor(Color newColor)
    {
        newColor.a = originalSuspiciousAlpha;
        suspiciousColor = newColor;
    }

    // ========================================================================
    // UTILITY METHODS
    // ========================================================================

    /// <summary>
    /// Creates a default transparent material for the vision cone.
    /// Uses Universal Render Pipeline if available, otherwise standard shader.
    /// </summary>
    private Material CreateDefaultMaterial()
    {
        Material mat;

        // Try URP shader first
        Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpShader != null)
        {
            mat = new Material(urpShader);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetFloat("_Surface", 1);  // Set to transparent
        }
        else
        {
            // Fallback to standard shader
            mat = new Material(Shader.Find("Standard"));
            mat.SetFloat("_Mode", 3);  // Transparent mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }

        return mat;
    }
}