using UnityEngine;
using UnityEngine.AI;

// ============================================================================
// GUARD - AI CHARACTER COMPONENT FOR VISION, HEARING, AND COMBAT
// ============================================================================
// 
// This component is attached to all AI characters (guards, zombies, etc.).
// It provides:
// 1. Vision system configuration (detection range, field of view)
// 2. Hearing system configuration (hearing range, sensitivity)
// 3. Combat settings (kill range)
// 4. Editor visualization (gizmos for debugging)
// 5. Brain integration (FSM, Behavior Tree, or Zombie AI)
//
// HOW IT WORKS:
// - The Guard holds configuration data (ranges, FOV)
// - The AIBrain (currentBrain) handles actual decision making
// - Guard updates brain every frame via Think()
// - CanSeePlayer property is updated by the brain when player is detected
//
// SEPARATION OF CONCERNS:
// - Guard = Data container + component holder
// - AIBrain = Decision making logic (FSM/BT/Zombie)
// - This allows easy swapping of AI types without changing the Guard
//
// ============================================================================

public class Guard : MonoBehaviour
{
    // ========================================================================
    // SERIALIZED FIELDS - CORE SETTINGS
    // ========================================================================

    [Header("=== GUARD CORE SETTINGS ===")]
    [Tooltip("Reference to the AI brain component (FSM, BTBrain, or Zombie)")]
    [SerializeField] public AIBrain currentBrain;

    [Tooltip("Maximum distance the guard can see the player (meters)")]
    [SerializeField] protected float detectionRange = 10f;

    [Tooltip("Field of view angle in degrees (0 = blind, 360 = full vision)")]
    [SerializeField] protected float fieldOfView = 60f;

    [Header("=== VISION SETTINGS ===")]
    [Tooltip("Transform where raycasts originate for vision detection")]
    [SerializeField] private Transform rayCastStartLocation;

    [Header("=== COMBAT SETTINGS ===")]
    [Tooltip("Distance at which guard can kill the player (meters)")]
    [SerializeField] protected float killRange = 2f;

    [Header("=== HEARING SETTINGS ===")]
    [Tooltip("Maximum distance the guard can hear noises (meters)")]
    [SerializeField] protected float maxHearingDistance = 15f;

    [Tooltip("Multiplier for hearing sensitivity (higher = better hearing)")]
    [SerializeField] public float guardHearingSensitivity = 5f;

    // ========================================================================
    // EDITOR VISUALIZATION SETTINGS
    // ========================================================================

    [Header("=== GIZMO VISUALIZATION ===")]
    [Tooltip("Show vision cone in Scene view")]
    [SerializeField] private bool showVisionCone = true;

    [Tooltip("Show hearing sphere in Scene view")]
    [SerializeField] private bool showHearingSphere = true;

    [Tooltip("Show kill range sphere in Scene view")]
    [SerializeField] private bool showKillRange = true;

    [Tooltip("Color of hearing range gizmo (alpha controls transparency)")]
    [SerializeField] private Color hearingColor = new Color(0f, 1f, 0f, 0.2f);

    [Tooltip("Color of vision range gizmo (alpha controls transparency)")]
    [SerializeField] private Color visionColor = new Color(1f, 1f, 0f, 0.15f);

    [Tooltip("Color of kill range gizmo (alpha controls transparency)")]
    [SerializeField] private Color killColor = new Color(1f, 0f, 0f, 0.2f);

    // ========================================================================
    // PRIVATE FIELDS - STORED ALPHA VALUES
    // ========================================================================

    // Store original alpha values from editor/prefab
    private float originalHearingAlpha;
    private float originalVisionAlpha;
    private float originalKillAlpha;
    private bool hasStoredOriginalAlphas = false;

    // ========================================================================
    // PUBLIC PROPERTIES
    // ========================================================================

    /// <summary>
    /// Gets the raycast start location transform for vision detection.
    /// Auto-created if not assigned in inspector.
    /// </summary>
    public Transform RayCastStartLocation => rayCastStartLocation;

    /// <summary>
    /// Indicates whether the guard can currently see the player.
    /// Updated by the AI brain each frame.
    /// </summary>
    public bool CanSeePlayer { get; set; } = false;

    // ========================================================================
    // GETTER METHODS (For AIBrain access)
    // ========================================================================

    public float GetDetectionRange() => detectionRange;
    public float GetFieldOfView() => fieldOfView;
    public float GetMaxHearingDistance() => maxHearingDistance;
    public float GetKillRange() => killRange;

    // ========================================================================
    // SETTER METHODS (For status effects like Blind)
    // ========================================================================

    /// <summary>
    /// Sets the detection range (used by Blind status effect).
    /// </summary>
    public void SetDetectionRange(float newRange) => detectionRange = newRange;

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    private void Start()
    {
        // Store original alpha values from editor/prefab
        StoreOriginalAlphaValues();

        // Auto-create raycast start location if not assigned
        if (rayCastStartLocation == null)
        {
            CreateRaycastStartLocation();
        }

        // Log guard setup information for debugging
        LogGuardSetup();

        // Initialize the AI brain
        InitializeBrain();
    }

    private void Update()
    {
        // Update the AI brain every frame
        if (currentBrain != null)
        {
            currentBrain.Think();
        }
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

        originalHearingAlpha = hearingColor.a;
        originalVisionAlpha = visionColor.a;
        originalKillAlpha = killColor.a;
        hasStoredOriginalAlphas = true;

        //Debug.Log($"[Guard] Stored original alphas - Hearing:{originalHearingAlpha}, Vision:{originalVisionAlpha}, Kill:{originalKillAlpha}");
    }

    /// <summary>
    /// Applies the stored alpha values to all gizmo colors.
    /// Called after color modifications to restore transparency.
    /// </summary>
    private void ApplyStoredAlphas()
    {
        hearingColor = new Color(hearingColor.r, hearingColor.g, hearingColor.b, originalHearingAlpha);
        visionColor = new Color(visionColor.r, visionColor.g, visionColor.b, originalVisionAlpha);
        killColor = new Color(killColor.r, killColor.g, killColor.b, originalKillAlpha);
    }

    /// <summary>
    /// Called when values change in the Unity Inspector.
    /// Ensures alpha values are preserved when editing colors.
    /// </summary>
    private void OnValidate()
    {
        // Only apply if we have stored values (prevents null reference)
        if (hasStoredOriginalAlphas)
        {
            ApplyStoredAlphas();
        }
    }

    // ========================================================================
    // INITIALIZATION METHODS
    // ========================================================================

    /// <summary>
    /// Creates a default raycast start location at eye level.
    /// Positioned at (0, 0.5, 0.5) relative to guard.
    /// </summary>
    private void CreateRaycastStartLocation()
    {
        GameObject startPoint = new GameObject("RayCastStart");
        startPoint.transform.SetParent(transform);
        startPoint.transform.localPosition = new Vector3(0, 0.5f, 0.5f);
        rayCastStartLocation = startPoint.transform;
        Debug.Log($"[Guard] Created raycast start location for {name}");
    }

    /// <summary>
    /// Logs guard configuration for debugging purposes.
    /// </summary>
    private void LogGuardSetup()
    {
        //Debug.Log($"<color=blue>[GUARD SETUP] {name}</color>");
        //Debug.Log($"  Hearing Range: {GetMaxHearingDistance()}m");
        //Debug.Log($"  Detection Range: {GetDetectionRange()}m");
        //Debug.Log($"  Field of View: {fieldOfView}°");
        //Debug.Log($"  Kill Range: {killRange}m");
        //Debug.Log($"  Position: {transform.position}");
    }

    /// <summary>
    /// Initializes the AI brain with this guard reference.
    /// </summary>
    private void InitializeBrain()
    {
        if (currentBrain != null)
        {
            currentBrain.Init(this);
        }
        else
        {
            Debug.LogWarning($"[Guard] No AI brain assigned to {name}!");
        }
    }

    // ========================================================================
    // PUBLIC METHODS - COLOR MODIFICATION
    // ========================================================================

    /// <summary>
    /// Sets the hearing color while preserving its original alpha.
    /// </summary>
    public void SetHearingColor(Color newColor)
    {
        newColor.a = originalHearingAlpha;
        hearingColor = newColor;
    }

    /// <summary>
    /// Sets the vision color while preserving its original alpha.
    /// </summary>
    public void SetVisionColor(Color newColor)
    {
        newColor.a = originalVisionAlpha;
        visionColor = newColor;
    }

    /// <summary>
    /// Sets the kill color while preserving its original alpha.
    /// </summary>
    public void SetKillColor(Color newColor)
    {
        newColor.a = originalKillAlpha;
        killColor = newColor;
    }

    // ========================================================================
    // EDITOR VISUALIZATION - GIZMOS
    // ========================================================================
    //
    // Gizmos are drawn in the Scene view for debugging purposes:
    // - GREEN sphere = Hearing range (where noises can be heard)
    // - YELLOW sphere = Vision detection range (max distance)
    // - CYAN cone = Vision field of view (angle)
    // - RED sphere = Kill range (distance to kill player)
    // - RED dot = Raycast start position (eye level)
    //
    // All colors preserve their alpha values from editor/prefab settings
    // =========================================================================

    private void OnDrawGizmos()
    {
        DrawHearingSphere();
        DrawKillRange();
        DrawDetectionRange();
        DrawVisionCone();
        DrawRaycastStartPoint();
        DrawEditorLabels();
    }

    /// <summary>
    /// Draws the hearing range as a semi-transparent green sphere.
    /// This is the most important debug visualization for noise detection.
    /// Preserves the alpha value set in editor/prefab.
    /// </summary>
    private void DrawHearingSphere()
    {
        if (!showHearingSphere) return;

        // Wireframe sphere for clear boundaries (uses original color with alpha)
        Gizmos.color = hearingColor;
        Gizmos.DrawWireSphere(transform.position, maxHearingDistance);

        // Semi-transparent fill for visual feedback (uses same alpha but more transparent)
        Color fillColor = new Color(hearingColor.r, hearingColor.g, hearingColor.b, hearingColor.a * 0.25f);
        Gizmos.color = fillColor;
        Gizmos.DrawSphere(transform.position, maxHearingDistance);
    }

    /// <summary>
    /// Draws the kill range as a semi-transparent red sphere.
    /// Preserves the alpha value set in editor/prefab.
    /// </summary>
    private void DrawKillRange()
    {
        if (!showKillRange) return;

        Gizmos.color = killColor;
        Gizmos.DrawWireSphere(transform.position, killRange);
    }

    /// <summary>
    /// Draws the detection range as a wireframe yellow sphere.
    /// Preserves the alpha value set in editor/prefab.
    /// </summary>
    private void DrawDetectionRange()
    {
        Gizmos.color = visionColor;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }

    /// <summary>
    /// Draws the vision cone using multiple line segments.
    /// Shows both the cone boundaries and filled area.
    /// </summary>
    private void DrawVisionCone()
    {
        if (!showVisionCone || rayCastStartLocation == null) return;

        float halfFOV = fieldOfView / 2f;
        float coneLength = detectionRange;
        Vector3 startPos = rayCastStartLocation.position;

        // Draw cone boundaries (cyan lines)
        DrawConeBoundaries(startPos, halfFOV, coneLength);

        // Draw cone fill (semi-transparent cyan with preserved alpha)
        DrawConeFill(startPos, halfFOV, coneLength);
    }

    /// <summary>
    /// Draws the left and right boundaries of the vision cone.
    /// </summary>
    private void DrawConeBoundaries(Vector3 startPos, float halfFOV, float coneLength)
    {
        Vector3 leftBoundary = Quaternion.Euler(0, -halfFOV, 0) * transform.forward * coneLength;
        Vector3 rightBoundary = Quaternion.Euler(0, halfFOV, 0) * transform.forward * coneLength;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(startPos, startPos + leftBoundary);
        Gizmos.DrawLine(startPos, startPos + rightBoundary);
    }

    /// <summary>
    /// Draws the filled area of the vision cone using triangles.
    /// Uses semi-transparent cyan with alpha preserved from vision color.
    /// </summary>
    private void DrawConeFill(Vector3 startPos, float halfFOV, float coneLength)
    {
        int segments = 20;
        float angleStep = fieldOfView / segments;

        // Draw outer arc
        Vector3 lastPoint = startPos + Quaternion.Euler(0, -halfFOV, 0) * transform.forward * coneLength;
        for (int i = 1; i <= segments; i++)
        {
            float currentAngle = -halfFOV + (i * angleStep);
            Vector3 currentPoint = startPos + Quaternion.Euler(0, currentAngle, 0) * transform.forward * coneLength;
            Gizmos.DrawLine(lastPoint, currentPoint);
            lastPoint = currentPoint;
        }

        // Draw filled triangles with alpha from vision color
        Color fillColor = new Color(0f, 1f, 1f, visionColor.a * 0.3f);
        Gizmos.color = fillColor;
        Vector3 prevPoint = startPos + Quaternion.Euler(0, -halfFOV, 0) * transform.forward * coneLength;
        for (int i = 1; i <= segments; i++)
        {
            float currentAngle = -halfFOV + (i * angleStep);
            Vector3 currentPoint = startPos + Quaternion.Euler(0, currentAngle, 0) * transform.forward * coneLength;
            DrawTriangle(startPos, prevPoint, currentPoint);
            prevPoint = currentPoint;
        }
    }

    /// <summary>
    /// Draws a single triangle for cone fill visualization.
    /// </summary>
    private void DrawTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, a);
    }

    /// <summary>
    /// Draws the raycast start point as a red wireframe sphere.
    /// </summary>
    private void DrawRaycastStartPoint()
    {
        if (rayCastStartLocation == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(rayCastStartLocation.position, 0.1f);
    }

    /// <summary>
    /// Draws text labels in the Scene view for range values.
    /// Only visible in Unity Editor.
    /// </summary>
    private void DrawEditorLabels()
    {
#if UNITY_EDITOR
        if (!showHearingSphere) return;

        // Label for hearing range
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * (maxHearingDistance + 0.5f),
            $"HEARING: {maxHearingDistance}m"
        );

        // Label for vision range
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * (detectionRange + 0.5f),
            $"VISION: {detectionRange:F1}m"
        );
#endif
    }

    /// <summary>
    /// Draws additional gizmos when the guard is selected.
    /// Keeps the Scene view cleaner when not selected.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Draw a white wireframe sphere for kill range when selected
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, killRange);
    }
}