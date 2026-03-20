using UnityEngine;

public class NoiseVisual : MonoBehaviour
{
    [Header("Visual Settings")]
    public float expandSpeed = 3f;
    public float targetRadius = 2f;
    public bool stayOnGround = true;

    [Header("Circle Settings")]
    public bool useFlatCircle = true;
    public float height = 0.1f; // Very flat

    private Material material;
    private float currentRadius = 0f;
    public Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;

        // Position at ground level
        if (stayOnGround)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out hit, 10f))
            {
                Vector3 pos = transform.position;
                pos.y = hit.point.y + 0.05f; // Just above ground
                transform.position = pos;
            }
        }

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            material = renderer.material;
            material.SetFloat("_Surface", 1);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        currentRadius += expandSpeed * Time.deltaTime;

        if (useFlatCircle)
        {
            // Flat circle on ground (X and Z scale, Y is flat)
            float diameter = currentRadius * 2;
            transform.localScale = new Vector3(diameter, height, diameter);
        }
        else
        {
            // Original sphere
            float diameter = currentRadius * 2;
            transform.localScale = new Vector3(diameter, diameter, diameter);
        }

        // Fade out based on progress
        if (material != null)
        {
            float progress = currentRadius / targetRadius;
            float alpha = Mathf.Lerp(1f, 0f, progress);
            Color color = material.color;
            color.a = alpha;
            material.color = color;
        }

        if (currentRadius >= targetRadius)
        {
            Destroy(gameObject);
        }
    }

    public void SetNoiseProperties(float radius, Color color)
    {
        targetRadius = radius;
        expandSpeed = radius * 1.5f;

        if (material != null)
        {
            material.color = color;
        }
    }
}