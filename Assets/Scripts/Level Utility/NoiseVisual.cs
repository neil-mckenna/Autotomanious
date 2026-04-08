using UnityEngine;

// ============================================================================
// NOISE VISUAL - VISUAL REPRESENTATION OF PLAYER NOISE FOR AI HEARING
// ============================================================================
// 
// This script creates a visual ring effect that expands outward from the player
// when they make noise (walking, running, jumping, landing). It serves as:
// 1. Player feedback - shows the player how much noise they're making
// 2. Debug visualization - helps designers tune noise radius values
// 3. Game feel - provides satisfying visual feedback for player actions
//
// FEATURES:
// - Expanding ring animation that scales from zero to target radius
// - Color coding based on noise type (green=yellow=run, cyan=jump, magenta=land)
// - Volume affects ring size and transparency (quieter = smaller + more transparent)
// - Ground snapping - rings appear flush with the ground
// - Optional flat circle or sphere visualization
// - Fades out as it expands for smooth disappearance
//
// OBJECT POOLING COMPATIBLE:
// - ResetAndPlay() prepares the object for reuse from a pool
// - Automatically disables itself when animation completes
// - Player script handles returning to pool
//
// ============================================================================

public class NoiseVisual : MonoBehaviour
{
    // ========================================================================
    // SERIALIZED FIELDS - CONFIGURE IN UNITY INSPECTOR
    // ========================================================================

    [Header("=== VISUAL SETTINGS ===")]
    [Tooltip("How fast the ring expands (meters per second)")]
    public float expandSpeed = 3f;

    [Tooltip("Maximum radius of the ring (meters)")]
    public float targetRadius = 2f;

    [Tooltip("Snap the ring to ground level? (Recommended: true)")]
    public bool stayOnGround = true;

    [Header("=== CIRCLE SETTINGS ===")]
    [Tooltip("Use a flat circle (best for ground). False = sphere")]
    public bool useFlatCircle = true;

    [Tooltip("Height of the flat circle when useFlatCircle is true")]
    public float height = 0.1f;

    // ========================================================================
    // PRIVATE FIELDS
    // ========================================================================

    private Material material;          // Material for color and transparency
    private float currentRadius = 0f;   // Current expansion radius
    private bool isPlaying = false;     // Is the animation currently playing?
    private new Renderer renderer;          // Reference to the renderer component
    private Color originalColor;        // Store original color for reset
    private Vector3 targetScale;        // Target scale for expansion

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    void Awake()
    {
        // Get and store renderer and material references
        renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            material = renderer.material;
            originalColor = material.color;
        }

        // Start disabled - will be activated by pool when needed
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isPlaying) return;

        // Expand the ring over time
        currentRadius += expandSpeed * Time.deltaTime;

        // Update scale based on current radius
        UpdateScale();

        // Fade out as it expands
        UpdateAlpha();

        // Auto-disable when animation completes (ready for pool return)
        if (currentRadius >= targetRadius)
        {
            isPlaying = false;
            gameObject.SetActive(false);
        }
    }

    // ========================================================================
    // PUBLIC METHODS - CALLED BY PLAYER SCRIPT
    // ========================================================================

    /// <summary>
    /// Resets the noise visual and starts the animation.
    /// Called by Player when retrieving from object pool.
    /// </summary>
    /// <param name="radius">Maximum radius of the ring (meters)</param>
    /// <param name="color">Color of the ring (based on noise type)</param>
    /// <param name="volume">Volume of the noise (0-1, affects size and transparency)</param>
    public void ResetAndPlay(float radius, Color color, float volume = 1f)
    {
        // Reset state
        currentRadius = 0f;
        targetRadius = radius;
        expandSpeed = radius * 1.5f;  // Larger rings expand faster
        isPlaying = true;

        // Calculate scale based on volume (quieter = smaller ring)
        // Volume 0.3 (quiet) -> scale 0.5 of max radius
        // Volume 1.0 (loud)  -> scale 1.0 of max radius
        float volumeScale = Mathf.Lerp(0.5f, 1.0f, volume);
        float finalScale = radius * volumeScale;
        targetScale = Vector3.one * finalScale;

        // Start from zero for expansion animation
        transform.localScale = Vector3.zero;

        // Set alpha based on volume (quieter = more transparent)
        color.a = Mathf.Lerp(0.3f, 0.9f, volume);

        // Snap to ground if enabled
        if (stayOnGround)
        {
            SnapToGround();
        }

        // Apply color to material
        if (material != null)
        {
            material.color = color;
            originalColor = color;
        }

        // Enable the GameObject
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Sets noise properties without playing the animation.
    /// Useful for initialization or manual control.
    /// </summary>
    public void SetNoiseProperties(float radius, Color color)
    {
        targetRadius = radius;
        expandSpeed = radius * 1.5f;
        originalColor = color;

        if (material != null)
        {
            material.color = color;
        }
    }

    // ========================================================================
    // PRIVATE METHODS - ANIMATION
    // ========================================================================

    /// <summary>
    /// Updates the scale of the ring based on current radius.
    /// Supports both flat circle (cylinder) and sphere modes.
    /// </summary>
    private void UpdateScale()
    {
        float diameter = currentRadius * 2;

        if (useFlatCircle)
        {
            // Flat circle (cylinder shape) - good for ground effects
            transform.localScale = new Vector3(diameter, height, diameter);
        }
        else
        {
            // Sphere shape - good for 3D effects
            transform.localScale = new Vector3(diameter, diameter, diameter);
        }
    }

    /// <summary>
    /// Updates the alpha transparency as the ring expands.
    /// Ring fades out smoothly as it reaches max radius.
    /// </summary>
    private void UpdateAlpha()
    {
        if (material == null) return;

        float progress = currentRadius / targetRadius;
        float alpha = Mathf.Lerp(1f, 0f, progress);

        Color color = material.color;
        color.a = alpha;
        material.color = color;
    }

    /// <summary>
    /// Snaps the ring to the ground using raycast.
    /// Prevents rings from floating above or sinking below terrain.
    /// </summary>
    private void SnapToGround()
    {
        RaycastHit hit;
        Vector3 rayStart = transform.position + Vector3.up * 2f;

        if (Physics.Raycast(rayStart, Vector3.down, out hit, 5f))
        {
            Vector3 pos = transform.position;
            pos.y = hit.point.y + 0.05f;  // Small offset to prevent z-fighting
            transform.position = pos;
        }
    }

    // ========================================================================
    // DEBUG VISUALIZATION (Optional)
    // ========================================================================

    private void OnDrawGizmosSelected()
    {
        if (!isPlaying && Application.isPlaying) return;

        // Draw ring radius in editor when selected
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, currentRadius);

        // Draw target radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, targetRadius);
    }
}