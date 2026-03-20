using UnityEngine;

public class VisionCone : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float heightOffset = 0.5f;
    [SerializeField] private float distanceScale = 0.3f;
    [SerializeField] private bool testMode = true; // Add this for testing

    [Header("Colors")]
    [SerializeField] private Color idleColor = new Color(1f, 0.5f, 0f, 0.2f);
    [SerializeField] private Color chasingColor = new Color(1f, 0f, 0f, 0.4f);

    private Guard guard;
    private SpriteRenderer spriteRenderer;
    private Transform coneVisual;

    private void Start()
    {
        // Try to find guard, but don't fail if not found
        guard = GetComponentInParent<Guard>();

        // Find the child with Sprite Renderer
        if (transform.childCount > 0)
        {
            coneVisual = transform.GetChild(0);
            spriteRenderer = coneVisual.GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer == null)
        {
            Debug.LogError("VisionCone: No SpriteRenderer found on child!");
            return;
        }

        // Set initial color
        spriteRenderer.color = idleColor;

        Debug.Log("VisionCone initialized in test mode");
    }

    private void Update()
    {
        if (spriteRenderer == null) return;

        if (testMode)
        {
            // In test mode, just spin slowly so you can see it
            transform.Rotate(Vector3.up, 30 * Time.deltaTime);

            // Pulse the scale for effect
            float pulse = Mathf.Sin(Time.time * 2f) * 0.2f + 1f;
            coneVisual.localScale = Vector3.one * (2f * pulse);
        }
        else if (guard != null)
        {
            // Normal mode - follow guard
            transform.position = guard.transform.position + Vector3.up * heightOffset;
            transform.rotation = guard.transform.rotation;

            float range = guard.GetDetectionRange();
            coneVisual.localScale = Vector3.one * (range * distanceScale);

            // Update color based on player detection
            spriteRenderer.color = guard.HasLineOfSightToPlayer() ? chasingColor : idleColor;
        }
    }
}