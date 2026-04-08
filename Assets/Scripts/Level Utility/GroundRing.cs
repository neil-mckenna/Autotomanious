using UnityEngine;

public class RingEffect : MonoBehaviour
{
    [Header("Ring Settings")]
    [SerializeField] private float expandSpeed = 5f;
    //[SerializeField] private float fadeSpeed = 2f;
    [SerializeField] private float maxSize = 10f;

    private Material material;
    private float currentSize = 0f;

    void Start()
    {
        material = GetComponentInChildren<Renderer>().material;
        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        // Expand
        currentSize += expandSpeed * Time.deltaTime;
        float scale = currentSize;
        transform.localScale = new Vector3(scale, scale, 1);

        // Fade
        if (material != null)
        {
            Color color = material.color;
            color.a = Mathf.Lerp(1f, 0f, currentSize / maxSize);
            material.color = color;
        }

        // Destroy when done
        if (currentSize >= maxSize)
        {
            Destroy(gameObject);
        }
    }
}