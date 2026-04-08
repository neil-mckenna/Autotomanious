using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================================
// PLAYER - MAIN CHARACTER CONTROLLER AND NOISE EMISSION SYSTEM
// ============================================================================
// 
// This component manages the player character with the following features:
// 1. Death and respawn logic
// 2. Noise emission for AI hearing detection
// 3. Visual noise indicators (rings that appear at foot position)
// 4. Object pooling for noise visuals (performance optimization)
// 5. Integration with GameManager for stats tracking
//
// NOISE SYSTEM OVERVIEW:
// - Walking, running, jumping, and landing all emit noise
// - Louder actions (running) have larger radius and volume
// - Visual rings appear at foot position to show noise radius
// - Guards within hearing range will investigate the noise
//
// OBJECT POOLING:
// - Noise visuals are pooled to reduce garbage collection
// - Prevents instantiate/destroy spam during gameplay
// - Pool expands automatically if needed
//
// ============================================================================

public class Player : MonoBehaviour
{
    // ========================================================================
    // PRIVATE REFERENCES
    // ========================================================================

    private GameManager gameManager;    // Reference to game manager for stats
    private bool isDead = false;        // Is the player currently dead?
    private Drive drive;                // Reference to movement controller

    // ========================================================================
    // SERIALIZED FIELDS - NOISE VISUALS
    // ========================================================================

    [Header("=== NOISE VISUAL SETTINGS ===")]
    [Tooltip("Prefab for noise ring visual (pooled for performance)")]
    public GameObject noiseVisualPrefab;

    [Tooltip("Transform at player's feet where noise originates")]
    public Transform footLocation;

    [Header("=== OBJECT POOLING SETTINGS ===")]
    [Tooltip("Number of noise rings to pre-instantiate in pool")]
    public int poolSize = 5;

    [SerializeField] public float feetNoiseOffsetY = 2f;

    // ========================================================================
    // NOISE POOLING FIELDS
    // ========================================================================

    private Queue<GameObject> noisePool = new Queue<GameObject>();     // Pool of inactive noise rings
    private List<GameObject> activeNoises = new List<GameObject>();    // Currently active noise rings

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    private void OnEnable()
    {
        isDead = false;
        FindGameManager();
        ResetPlayer();
    }

    private void Start()
    {
        RegisterWithGameManager();
        SetupFootLocation();
        InitializeNoisePool();
    }

    // ========================================================================
    // INITIALIZATION METHODS
    // ========================================================================

    /// <summary>
    /// Finds and registers with GameManager.
    /// Attempts multiple methods to ensure registration succeeds.
    /// </summary>
    private void RegisterWithGameManager()
    {
        gameManager = Object.FindAnyObjectByType<GameManager>();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPlayer(this);
        }
        else
        {
            Debug.LogError("GameManager instance not found! Player registration failed.");
            GameManager gm = FindAnyObjectByType<GameManager>();
            if (gm != null)
            {
                gm.RegisterPlayer(this);
            }
        }
    }

    /// <summary>
    /// Gets foot location reference from Drive component.
    /// Foot location is where noise rings spawn from.
    /// </summary>
    private void SetupFootLocation()
    {
        drive = GetComponent<Drive>();

        if (drive != null)
        {
            footLocation = drive.FootLocation;
        }
        else
        {
            Debug.LogWarning("[Player] No Drive component found! Foot location will be null.");
        }
    }

    /// <summary>
    /// Initializes the object pool for noise visuals.
    /// Pre-instantiates noise rings for performance.
    /// </summary>
    private void InitializeNoisePool()
    {
        if (noiseVisualPrefab == null)
        {
            Debug.LogWarning("[Player] NoiseVisualPrefab not assigned! Will use simple cylinder rings.");
            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            GameObject noise = Instantiate(noiseVisualPrefab);
            noise.SetActive(false);
            noisePool.Enqueue(noise);
        }

        Debug.Log($"[Player] Noise pool initialized with {poolSize} objects");
    }

    /// <summary>
    /// Finds GameManager reference (called if missing).
    /// </summary>
    private void FindGameManager()
    {
        gameManager = Object.FindAnyObjectByType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogWarning("[Player] GameManager not found, will try again later");
        }
    }

    // ========================================================================
    // NOISE EMISSION SYSTEM
    // ========================================================================
    //
    // Called by Drive component when player makes noise.
    // Parameters:
    // - position: World position where noise originated (foot location)
    // - radius: How far the noise travels (meters)
    // - noiseType: walking, running, jump, landing
    // - volume: Loudness (0-1) affects detection range
    //
    // =========================================================================

    /// <summary>
    /// Emits a noise that can be heard by guards.
    /// Creates visual ring and notifies all guards within range.
    /// </summary>
    public void MakeNoise(Vector3 position, float radius, string noiseType, float volume = 1f)
    {
        if (isDead) return;

        // Adjust Y position to ground level for visual consistency
        Vector3 noisePosition = position;
        noisePosition.y = 0.05f - feetNoiseOffsetY;  // Slightly above ground to prevent z-fighting

        // Log noise emission for debugging
        LogNoiseEmission(noisePosition, radius, noiseType, volume);

        // Spawn visual indicator
        SpawnNoiseVisual(noisePosition, radius, noiseType, volume);

        // Find all guards and check if they can hear this noise
        NotifyGuardsOfNoise(noisePosition, radius, noiseType, volume);
    }

    /// <summary>
    /// Logs noise emission details to console for debugging.
    /// </summary>
    private void LogNoiseEmission(Vector3 position, float radius, string noiseType, float volume)
    {
        //Debug.Log($"===== NOISE EMITTED =====");
        //Debug.Log($"Type: {noiseType}, Position: {position}, Radius: {radius}m, Volume: {volume:F2}");
    }

    /// <summary>
    /// Notifies all guards about the noise if they can hear it.
    /// Distance check is performed here, not in the guard.
    /// </summary>
    private void NotifyGuardsOfNoise(Vector3 noisePosition, float radius, string noiseType, float volume)
    {
        Guard[] allGuards = FindObjectsByType<Guard>(FindObjectsSortMode.None);
        int guardsHeard = 0;

        foreach (Guard guard in allGuards)
        {
            float distanceFromNoiseToGuard = Vector3.Distance(noisePosition, guard.transform.position);
            float guardHearingRange = guard.GetMaxHearingDistance();
            bool canHear = distanceFromNoiseToGuard <= guardHearingRange;

            //Debug.Log($"  Guard: {guard.name} | Distance: {distanceFromNoiseToGuard:F2}m | Range: {guardHearingRange:F2}m | Can Hear: {(canHear ? "YES" : "NO")}");

            if (canHear)
            {
                guardsHeard++;
                AIBrain brain = guard.currentBrain;
                if (brain != null)
                {
                    // Pass pre-calculated distance for performance
                    brain.HearNoise(noisePosition, distanceFromNoiseToGuard, radius, noiseType, volume);
                }
            }
        }

        //Debug.Log($"Total guards that can hear: {guardsHeard}/{allGuards.Length}");
    }

    // ========================================================================
    // NOISE VISUAL SYSTEM
    // ========================================================================

    /// <summary>
    /// Spawns a visual noise ring at the specified position.
    /// Uses object pooling if prefab is available, otherwise creates simple cylinder.
    /// </summary>
    private void SpawnNoiseVisual(Vector3 position, float radius, string noiseType, float volume)
    {
        if (noiseVisualPrefab == null)
        {
            CreateSimpleRing(position, radius, noiseType, volume);
            return;
        }

        // Get a noise ring from the object pool
        GameObject noiseRing = GetNoiseRingFromPool();
        if (noiseRing != null)
        {
            noiseRing.transform.position = position;
            noiseRing.transform.SetParent(null);  // Detach from player
            noiseRing.SetActive(true);

            // Configure the noise visual
            NoiseVisual visual = noiseRing.GetComponent<NoiseVisual>();
            if (visual != null)
            {
                Color noiseColor = GetNoiseColor(noiseType);
                // Alpha scales with volume (quieter = more transparent)
                noiseColor.a = 0.3f + (volume * 0.5f);
                visual.ResetAndPlay(radius, noiseColor);
            }

            activeNoises.Add(noiseRing);
            StartCoroutine(ReturnNoiseToPool(noiseRing, 2f));
        }
    }

    /// <summary>
    /// Creates a simple cylinder ring when no prefab is assigned.
    /// Used as a fallback visualization.
    /// </summary>
    private void CreateSimpleRing(Vector3 position, float radius, string noiseType, float volume)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "SimpleNoiseRing";
        ring.transform.position = new Vector3(position.x, 0.05f, position.z);
        ring.transform.localScale = new Vector3(radius * 2, 0.05f, radius * 2);

        // Remove collider so it doesn't block movement
        Destroy(ring.GetComponent<Collider>());

        // Configure material color
        Renderer renderer = ring.GetComponent<Renderer>();
        Color color = GetNoiseColor(noiseType);
        color.a = 0.3f + (volume * 0.5f);  // Alpha based on volume

        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        renderer.material = mat;

        StartCoroutine(FadeAndDestroy(ring, 1f));
    }

    /// <summary>
    /// Gets a noise ring from the object pool.
    /// Expands pool if empty (prevents errors).
    /// </summary>
    private GameObject GetNoiseRingFromPool()
    {
        if (noisePool.Count > 0)
        {
            return noisePool.Dequeue();
        }

        // Pool empty - create new one (expand pool dynamically)
        //Debug.LogWarning("[Player] Pool empty, creating new noise ring (expanding pool)");
        GameObject newNoise = Instantiate(noiseVisualPrefab);
        return newNoise;
    }

    /// <summary>
    /// Returns a noise ring to the pool after delay.
    /// Allows ring to finish its animation before recycling.
    /// </summary>
    private IEnumerator ReturnNoiseToPool(GameObject noise, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (noise != null)
        {
            noise.SetActive(false);
            noisePool.Enqueue(noise);
            activeNoises.Remove(noise);
        }
    }

    /// <summary>
    /// Coroutine that fades out and destroys simple cylinder rings.
    /// Scales up while fading out for expanding ring effect.
    /// </summary>
    private IEnumerator FadeAndDestroy(GameObject obj, float duration)
    {
        float elapsed = 0f;
        Renderer renderer = obj.GetComponent<Renderer>();
        Color startColor = renderer.material.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Fade out alpha
            float alpha = Mathf.Lerp(1f, 0f, t);
            Color color = startColor;
            color.a = alpha;
            renderer.material.color = color;

            // Expand ring
            float scale = Mathf.Lerp(1f, 2f, t);
            obj.transform.localScale = new Vector3(scale, 0.05f, scale);

            yield return null;
        }

        Destroy(obj);
    }

    /// <summary>
    /// Returns color for noise type (visual feedback).
    /// - Walking: Green
    /// - Running: Yellow
    /// - Jump: Cyan
    /// - Landing: Magenta
    /// </summary>
    private Color GetNoiseColor(string noiseType)
    {
        switch (noiseType)
        {
            case "walking": return Color.green;
            case "running": return Color.yellow;
            case "jump": return Color.cyan;
            case "landing": return Color.magenta;
            default: return Color.white;
        }
    }

    // ========================================================================
    // DEATH & RESPAWN SYSTEM
    // ========================================================================

    /// <summary>
    /// Handles collision with guards - triggers death.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (isDead) return;

        if (other.gameObject.CompareTag("Guard"))
        {
            Die(other.gameObject.name, other.gameObject);
        }
    }

    /// <summary>
    /// Kills the player and notifies GameManager.
    /// Disables movement and visual components.
    /// </summary>
    public void Die(string killerName, GameObject go)
    {
        if (isDead) return;
        isDead = true;

        Debug.LogWarning($"Player died! Killed by {go.name} at {go.transform.position} - {killerName}");

        // Ensure GameManager reference exists
        if (gameManager == null)
        {
            gameManager = Object.FindAnyObjectByType<GameManager>();
        }

        // Notify GameManager of death
        if (gameManager != null)
        {
            gameManager.PlayerDied($"Touched By Guard {killerName}");
        }
        else
        {
            Debug.LogError("[Player] CRITICAL: Cannot find GameManager when player dies!");
        }

        DisablePlayer();
    }

    /// <summary>
    /// Disables player movement and visuals on death.
    /// </summary>
    private void DisablePlayer()
    {
        Drive drive = GetComponent<Drive>();
        if (drive != null)
            drive.enabled = false;
    }

    /// <summary>
    /// Resets the player after death (called by GameManager on respawn).
    /// Re-enables all components and visuals.
    /// </summary>
    public void ResetPlayer()
    {
        isDead = false;

        // Re-enable movement
        Drive drive = GetComponent<Drive>();
        if (drive != null)
        {
            drive.enabled = true;
        }

        // Re-enable all scripts
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            script.enabled = true;
        }

        // Re-enable renderers
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.enabled = true;
        }

        // Re-enable collider
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = true;
    }

    // ========================================================================
    // CLEANUP
    // ========================================================================

    /// <summary>
    /// Cleans up all noise visuals when player is disabled.
    /// Prevents memory leaks from active noise rings.
    /// </summary>
    private void OnDisable()
    {
        foreach (var noise in activeNoises)
        {
            if (noise != null)
                Destroy(noise);
        }
        activeNoises.Clear();
        noisePool.Clear();
    }
}