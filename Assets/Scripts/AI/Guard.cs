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

    
    public bool CanSeePlayer { get; set; } = false;

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
            startPoint.transform.localPosition = new Vector3(0, 0.5f, 0.5f);
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
            currentBrain.Think();
        }
    }

    // ========== FIXED DEBUG GIZMOS WITH DYNAMIC VALUES ==========
    private void OnDrawGizmos()
    {
        if (rayCastStartLocation != null)
        {
            // Draw raycast start point (RED sphere)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(rayCastStartLocation.position, 0.1f);

            // Draw detection range (YELLOW)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            // Draw FOV cone (CYAN)
            float halfFOV = fieldOfView / 2f;
            Vector3 leftBoundary = Quaternion.Euler(0, -halfFOV, 0) * transform.forward * detectionRange;
            Vector3 rightBoundary = Quaternion.Euler(0, halfFOV, 0) * transform.forward * detectionRange;
            Gizmos.DrawLine(rayCastStartLocation.position, rayCastStartLocation.position + leftBoundary);
            Gizmos.DrawLine(rayCastStartLocation.position, rayCastStartLocation.position + rightBoundary);
        }
    }

    private void DrawVisionCone()
    {
        float halfFOV = fieldOfView / 2f;
        float coneLength = detectionRange;
        
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f); // Semi-transparent cyan
        
        // Left boundary
        Vector3 leftBoundary = Quaternion.Euler(0, -halfFOV, 0) * transform.forward * coneLength;
        // Right boundary
        Vector3 rightBoundary = Quaternion.Euler(0, halfFOV, 0) * transform.forward * coneLength;
        
        // Draw cone lines
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary);
        
        // Draw arc (filled cone effect)
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
        
        // Draw filled cone area (semi-transparent)
        Gizmos.color = new Color(0f, 1f, 1f, 0.05f);
        Vector3 prevPoint = transform.position + Quaternion.Euler(0, -halfFOV, 0) * transform.forward * coneLength;
        for (int i = 1; i <= segments; i++)
        {
            float currentAngle = -halfFOV + (i * angleStep);
            Vector3 currentPoint = transform.position + Quaternion.Euler(0, currentAngle, 0) * transform.forward * coneLength;
            DrawTriangle(transform.position, prevPoint, currentPoint);
            prevPoint = currentPoint;
        }
    }
    
    private void DrawTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, a);
    }
}