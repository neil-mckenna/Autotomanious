using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 5f;
    public float height = 2f;

    [Header("Rotation Settings")]
    public bool lockRotation = true;
    public float smoothSpeed = 5f;

    private void LateUpdate()
    {
        if (target == null) return;

        // Position behind the player based on their forward direction
        Vector3 behindPosition = target.position - target.forward * distance + Vector3.up * height;

        // Smoothly move camera
        transform.position = Vector3.Lerp(transform.position, behindPosition, smoothSpeed * Time.deltaTime);

        // Make camera look in same direction as player
        transform.rotation = Quaternion.Lerp(transform.rotation, target.rotation, smoothSpeed * Time.deltaTime);
    }
}