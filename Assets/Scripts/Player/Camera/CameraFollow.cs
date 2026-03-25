using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 5f;
    public float height = 2f;
    public bool lockRotation = true;
    public float smoothSpeed = 5f;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 behindPosition = target.position - target.forward * distance + Vector3.up * height;
        transform.position = Vector3.Lerp(transform.position, behindPosition, smoothSpeed * Time.deltaTime);

        if (lockRotation)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, target.rotation, smoothSpeed * Time.deltaTime);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        // Snap to position immediately
        if (target != null)
        {
            transform.position = target.position - target.forward * distance + Vector3.up * height;
            transform.LookAt(target);
            Debug.Log($"Camera target set to {newTarget.name}");
        }
    }
}