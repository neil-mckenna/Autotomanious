using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DynamicVisionCone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Guard guard;

    [Header("Visual Settings")]
    [SerializeField] private Material coneMaterial;
    [SerializeField] private float heightOffset = 0.5f;
    [SerializeField] private int segments = 24;

    [Header("State Colors")]
    [SerializeField] private Color idleColor = new Color(0.5f, 0.8f, 1f, 0.1f);     // Light Blue - Patrolling/Wandering
    [SerializeField] private Color searchingColor = new Color(1f, 1f, 0f, 0.15f);  // Yellow - Searching/Suspicious
    [SerializeField] private Color chasingColor = new Color(1f, 0f, 0f, 0.2f);     // Red - Chasing player
    [SerializeField] private Color suspiciousColor = new Color(1f, 0f, 0.5f, 0.15f);    // Purple - Alert but no target

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh coneMesh;
    private float lastRange;
    private float lastFOV;

    private void Start()
    {
        if (guard == null)
            guard = GetComponentInParent<Guard>();

        if (guard == null)
        {
            Debug.LogError("DynamicVisionCone: No Guard found!");
            enabled = false;
            return;
        }

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (coneMaterial == null)
        {
            coneMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            coneMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            coneMaterial.SetFloat("_Surface", 1);
        }
        meshRenderer.material = coneMaterial;

        GenerateConeMesh();
        lastRange = guard.GetDetectionRange();
        lastFOV = guard.GetFieldOfView();
    }

    private void GenerateConeMesh()
    {
        if (guard == null) return;

        float range = guard.GetDetectionRange();
        float fov = guard.GetFieldOfView() * Mathf.Deg2Rad;

        coneMesh = new Mesh();

        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;

        float startAngle = -fov * 0.5f;
        float angleStep = fov / segments;

        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = startAngle + (i * angleStep);
            float x = Mathf.Sin(currentAngle) * range;
            float z = Mathf.Cos(currentAngle) * range;
            vertices[i + 1] = new Vector3(x, 0, z);
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        coneMesh.vertices = vertices;
        coneMesh.triangles = triangles;
        coneMesh.RecalculateNormals();
        meshFilter.mesh = coneMesh;
    }

    private void Update()
    {
        if (guard == null) return;

        // Position and rotation
        transform.position = guard.transform.position + Vector3.up * heightOffset;
        transform.rotation = guard.transform.rotation;

        // Check if range or FOV changed
        float currentRange = guard.GetDetectionRange();
        float currentFOV = guard.GetFieldOfView();

        if (Mathf.Abs(currentRange - lastRange) > 0.01f ||
            Mathf.Abs(currentFOV - lastFOV) > 0.01f)
        {
            GenerateConeMesh();
            lastRange = currentRange;
            lastFOV = currentFOV;
        }

        // Update color based on guard state
        UpdateConeColor();
    }

    private void UpdateConeColor()
    {
        Color targetColor = idleColor;

        // Check for Zombie brain first
        if (guard.currentBrain is Zombie zombieBrain)
        {
            // Get zombie's chasing state
            bool isChasing = zombieBrain.IsChasing();

            if (isChasing)
                targetColor = chasingColor; // Red
            else
                targetColor = idleColor; // Light Blue (or you could use a different color for zombies)
        }
        else if (guard.currentBrain is BTBrain btBrain)
        {
            string action = btBrain.GetCurrentAction();

            if (action == "Chasing")
                targetColor = chasingColor; // Red
            else if (action == "Searching")
                targetColor = searchingColor; // Yellow
            else if (action == "Suspicious")
                targetColor = suspiciousColor; // Purple
            else
                targetColor = idleColor; // Light Blue
        }
        else if (guard.currentBrain is FSM fsmBrain)
        {
            string state = fsmBrain.GetCurrentState();

            if (state == "Chasing")
                targetColor = chasingColor; // Red
            else if (state == "Searching")
                targetColor = searchingColor; // Yellow
            else if (state == "Suspicious")
                targetColor = suspiciousColor; // Purple
            else
                targetColor = idleColor; // Light Blue
        }

        meshRenderer.material.color = Color.Lerp(meshRenderer.material.color, targetColor, Time.deltaTime * 5f);
    }


}