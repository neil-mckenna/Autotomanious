using UnityEngine;
using UnityEngine.AI;

public class Guard : MonoBehaviour
{
    [Header("Guard Settings")]
    [SerializeField] public AIBrain currentBrain;
    [SerializeField] protected float detectionRange = 10f;
    [SerializeField] protected float fieldOfView = 60f;

    [Header("Vision")]
    [SerializeField] private Transform rayCastStartLocation;

    [SerializeField] public Transform RayCastStartLocation => rayCastStartLocation;

    [Header("Kill Settings")]
    [SerializeField] protected float killRange = 2f;

    [Header("Hearing Settings")]
    [SerializeField] protected float maxHearingDistance = 15f;
    [SerializeField] public float guardHearingSensitivity = 5f;

    [SerializeField] public bool CanSeePlayer;

    // Getters
    public float GetDetectionRange() => detectionRange;
    public float GetFieldOfView() => fieldOfView;
    public float GetMaxHearingDistance() => maxHearingDistance;
    public float GetKillRange() => killRange;



    public void SetDetectionRange(float newRange) => detectionRange = newRange;

    private void Start()
    {

        if (rayCastStartLocation == null)
        {
            GameObject startPoint = new GameObject("RayCastStart");
            startPoint.transform.SetParent(transform);
            startPoint.transform.localPosition = new Vector3(0, 1.5f, 0.5f);
            rayCastStartLocation = startPoint.transform;
            Debug.Log("Created raycast start location");
        }

        InitializeBrain();
    }

    private void InitializeBrain()
    {

        if (currentBrain != null)
        {
           
            currentBrain.Init(this);

        }
    }

    private void Update()
    {
        if (currentBrain != null)
        {

            CanSeePlayer = currentBrain.HasLineOfSightToPlayer();



            currentBrain.Think();
        }
        
    }

    // Debug Gizmos
    // Proper Gizmos drawing with correct color order
    private void OnDrawGizmos()
    {

        // Draw the direction to player vs forward direction
        if (currentBrain != null && currentBrain.Player != null && rayCastStartLocation != null)
        {
            Vector3 eyePos = rayCastStartLocation.position;
            Vector3 playerTarget = currentBrain.Player.transform.position + Vector3.up * 1.0f;
            Vector3 directionToPlayer = (playerTarget - eyePos).normalized;
            Vector3 forward = transform.forward;

            // Draw forward direction (cyan)
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(eyePos, forward * 3f);

            // Draw direction to player (magenta)
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(eyePos, directionToPlayer * 3f);

            // Draw angle arc
            float angle = Vector3.Angle(forward, directionToPlayer);

#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(eyePos + Vector3.up * 0.5f,
                $"Angle to player: {angle:F1}°\nForward: {forward}\nToPlayer: {directionToPlayer}");
#endif

            // Draw text at player position
            if (angle > 45f)
            {
                UnityEditor.Handles.color = Color.red;
                UnityEditor.Handles.Label(playerTarget + Vector3.up * 0.5f,
                    $"Player is {angle:F1}° to the side!\nGuard needs to turn!");
            }
        }


        // audio checks

        // Draw hearing range (bright pink) - very visible!
        Gizmos.color = new Color(1f, 0.2f, 0.8f, 0.15f); // Hot pink
        Gizmos.DrawWireSphere(transform.position, maxHearingDistance);

        // Draw inner ring (magenta) to show effective hearing range
        Gizmos.color = new Color(1f, 0f, 1f, 0.4f); // Bright magenta
        Gizmos.DrawWireSphere(transform.position, maxHearingDistance);
    }

    private void DrawVisionCone()
    {
        float halfFOV = fieldOfView / 2f;
        float coneLength = detectionRange;

        // Set color before drawing
        Gizmos.color = Color.cyan;

        // Left boundary
        Vector3 leftBoundary = Quaternion.Euler(0, -halfFOV, 0) * transform.forward * coneLength;
        // Right boundary
        Vector3 rightBoundary = Quaternion.Euler(0, halfFOV, 0) * transform.forward * coneLength;

        // Draw cone lines
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary);

        // Draw arc (circle segment)
        int segments = 20;
        float angleStep = fieldOfView / segments;
        Vector3 lastPoint = transform.position + Quaternion.Euler(0, -halfFOV, 0) * transform.forward * coneLength;

        for (int i = 1; i <= segments; i++)
        {
            float currentAngle = -halfFOV + (i * angleStep);
            Vector3 currentPoint = transform.position + Quaternion.Euler(0, currentAngle, 0) * transform.forward * coneLength;
            Gizmos.DrawLine(lastPoint, currentPoint);
            lastPoint = currentPoint;
        }
    }

}