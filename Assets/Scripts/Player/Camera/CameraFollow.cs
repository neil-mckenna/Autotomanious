using UnityEngine;
using UnityEngine.InputSystem;

public class FollowCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 5f;
    public float height = 2.5f;

    [Header("Zoom Settings")]
    public float minDistance = 3f;
    public float maxDistance = 10f;
    public float zoomSpeed = 2f;
    public bool invertZoom = true;

    [Header("Rotation Settings")]
    public bool lockRotation = true;
    public float smoothSpeed = 5f;

    private float currentDistance;
    private Camera cam;
    private Mouse mouse;

    private void Start()
    {
        cam = GetComponent<Camera>();
        currentDistance = distance;
        mouse = Mouse.current; // Get mouse reference for new input system

        if (mouse == null)
        {
            Debug.LogError("Mouse not found! Make sure Input System is properly set up.");
        }
    }

    private void LateUpdate()
    {
        if (target == null || mouse == null) return;

        // Handle zoom input using new input system
        float scrollValue = mouse.scroll.y.ReadValue();

        if (Mathf.Abs(scrollValue) > 0.01f)
        {
            // Apply zoom (scroll up = zoom in = decrease distance)
            float direction = invertZoom ? -scrollValue : scrollValue;
            currentDistance += direction * zoomSpeed;

            // Clamp to limits
            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);

            Debug.Log($"Camera distance: {currentDistance:F1} (Scroll: {scrollValue})");
        }

        // Position behind the player based on their forward direction
        Vector3 behindPosition = target.position - target.forward * currentDistance + Vector3.up * height;

        // Smoothly move camera
        transform.position = Vector3.Lerp(transform.position, behindPosition, smoothSpeed * Time.deltaTime);

        // Make camera look in same direction as player
        transform.rotation = Quaternion.Lerp(transform.rotation, target.rotation, smoothSpeed * Time.deltaTime);
    }
}