using UnityEngine;

public class VisionCone : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float heightOffset = 0.5f;
    [SerializeField] private float distanceScale = 0.3f;
    [SerializeField] private LayerMask obstacleLayerMask = -1; // What layers block vision
    [SerializeField] private bool enableObstacleBlocking = true;
    [SerializeField] private int raycastSegments = 36; // Higher = smoother cone edges

    [Header("Colors")]
    [SerializeField] private Color idleColor = new Color(1f, 0.5f, 0f, 0.2f);
    [SerializeField] private Color chasingColor = new Color(1f, 0f, 0f, 0.4f);
    [SerializeField] private Color blockedColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
    [SerializeField] private Color suspiciousColor = new Color(1f, 1f, 0f, 0.3f);

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private Guard guard;
    private SpriteRenderer spriteRenderer;
    private Transform coneVisual;
    private Material coneMaterial;
    private FSM fsm;
    private MeshFilter coneMeshFilter;

    private void Start()
    {
        guard = GetComponentInParent<Guard>();
        fsm = guard?.currentBrain as FSM;

        if (transform.childCount > 0)
        {
            coneVisual = transform.GetChild(0);
            spriteRenderer = coneVisual.GetComponent<SpriteRenderer>();

            // If using sprite renderer, we need to create a mesh for dynamic shape
            if (spriteRenderer != null && enableObstacleBlocking)
            {
                // Switch to mesh for dynamic cone shape
                CreateDynamicConeMesh();
            }
        }

        if (spriteRenderer == null)
        {
            Debug.LogError("VisionCone: No SpriteRenderer found on child!");
            return;
        }

        spriteRenderer.color = idleColor;
        Debug.Log("VisionCone initialized");
    }

    private void CreateDynamicConeMesh()
    {
        // Add MeshFilter and MeshRenderer if using dynamic cone
        coneMeshFilter = coneVisual.gameObject.GetComponent<MeshFilter>();
        if (coneMeshFilter == null)
        {
            coneMeshFilter = coneVisual.gameObject.AddComponent<MeshFilter>();
        }

        MeshRenderer meshRenderer = coneVisual.gameObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = coneVisual.gameObject.AddComponent<MeshRenderer>();
        }

        // Copy material from sprite renderer
        if (spriteRenderer != null)
        {
            meshRenderer.material = spriteRenderer.material;
            Destroy(spriteRenderer);
        }

        coneMaterial = meshRenderer.material;
    }

    private void Update()
    {
        if (!enableObstacleBlocking || coneMeshFilter == null)
        {
            // Use simple scaling if no obstacle blocking
            UpdateSimpleCone();
        }
        else
        {
            // Update dynamic cone that respects obstacles
            UpdateDynamicCone();
        }
    }

    private void UpdateSimpleCone()
    {
        if (spriteRenderer == null || guard == null) return;

        // Follow guard position and rotation
        transform.position = guard.transform.position + Vector3.up * heightOffset;
        transform.rotation = guard.transform.rotation;

        // Scale cone to match detection range
        float range = guard.GetDetectionRange();
        coneVisual.localScale = Vector3.one * (range * distanceScale);

        // Update color based on state
        UpdateConeColor();
    }

    private void UpdateDynamicCone()
    {
        if (guard == null) return;

        // Follow guard position and rotation
        transform.position = guard.transform.position + Vector3.up * heightOffset;
        transform.rotation = guard.transform.rotation;

        float detectionRange = guard.GetDetectionRange();
        float halfFOV = guard.GetFieldOfView() / 2f;

        // Raycast to find obstacles and create blocked cone shape
        Mesh mesh = BuildBlockedConeMesh(detectionRange, halfFOV);
        coneMeshFilter.mesh = mesh;

        UpdateConeColor();
    }

    private Mesh BuildBlockedConeMesh(float maxRange, float halfFOV)
    {
        Mesh mesh = new Mesh();

        Vector3 eyePosition = transform.position;
        Vector3 forward = transform.forward;

        // Calculate raycast directions
        int segments = raycastSegments;
        float angleStep = guard.GetFieldOfView() / segments;
        float startAngle = -halfFOV;

        Vector3[] vertices = new Vector3[segments + 2];
        Vector2[] uv = new Vector2[segments + 2];
        int[] triangles = new int[segments * 3];

        // First vertex is the origin (0,0)
        vertices[0] = Vector3.zero;
        uv[0] = new Vector2(0.5f, 0);

        // Raycast to find hit distances for each angle
        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = startAngle + (i * angleStep);
            Vector3 direction = Quaternion.Euler(0, currentAngle, 0) * forward;

            float hitDistance = maxRange;
            RaycastHit hit;

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

            // Calculate vertex position (in local space)
            Vector3 vertexPos = direction * hitDistance;
            vertices[i + 1] = vertexPos;
            uv[i + 1] = new Vector2((float)i / segments, 1);
        }

        // Build triangles
        for (int i = 0; i < segments; i++)
        {
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        return mesh;
    }

    private void UpdateConeColor()
    {
        if (spriteRenderer == null && coneMaterial == null) return;

        bool canSeePlayer = guard.CanSeePlayer;
        string currentState = GetGuardState();

        Color targetColor;

        if (canSeePlayer)
        {
            targetColor = chasingColor;
        }
        else if (currentState == "Suspicious")
        {
            targetColor = suspiciousColor;
        }
        else
        {
            targetColor = idleColor;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = targetColor;
        }
        else if (coneMaterial != null)
        {
            coneMaterial.color = targetColor;
        }
    }

    private string GetGuardState()
    {
        if (fsm != null)
            return fsm.GetCurrentState();
        return "Unknown";
    }

    private void OnDrawGizmosSelected()
    {
        if (!enableObstacleBlocking || guard == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
}