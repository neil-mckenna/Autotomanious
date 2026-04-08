using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// ============================================================================
// ZOMBIE SPAWNER - MANAGES ZOMBIE SPAWNING WITH WAVE SYSTEM
// ============================================================================
// 
// This script handles spawning zombies with safety checks and wave management.
// Features:
// 1. Wave-based spawning with configurable zombies per wave
// 2. Safe spawn position detection (avoids player, ensures NavMesh)
// 3. Player proximity check (won't spawn too close)
// 4. Zombie tracking and cleanup
// 5. Visual spawn radius for editor setup
// 6. Maximum zombie limit enforcement
//
// HOW TO USE:
// - Place spawner at desired location in scene
// - Configure spawn radius and safety distances
// - Assign zombie prefab and brain prefab
// - Enable/disable wave spawning as needed
//
// ============================================================================

public class ZombieSpawner : MonoBehaviour
{
    // ========================================================================
    // SERIALIZED FIELDS - CORE SETTINGS
    // ========================================================================

    [Header("=== ZOMBIE PREFABS ===")]
    [Tooltip("The zombie GameObject prefab (contains model, collider, etc.)")]
    [SerializeField] private GameObject zombiePrefab;

    [Tooltip("Prefab containing the Zombie AI brain (attaches to zombie)")]
    [SerializeField] private AIBrain zombieBrainPrefab;

    [Header("=== SPAWNER LIMITS ===")]
    [Tooltip("Maximum number of zombies that can exist at once")]
    [SerializeField] private int maxZombies = 10;

    [Tooltip("Radius around spawner where zombies can appear")]
    [SerializeField] private float spawnRadius = 8f;

    [Tooltip("Time between spawn attempts (seconds)")]
    [SerializeField] private float spawnInterval = 5f;

    [Tooltip("Should zombies spawn automatically on Start?")]
    [SerializeField] private bool spawnOnStart = true;

    [Header("=== SPAWN SAFETY ===")]
    [Tooltip("Minimum distance zombies must be from player when spawning")]
    [SerializeField] private float minDistanceFromPlayer = 5f;

    [Tooltip("Maximum attempts to find valid spawn position")]
    [SerializeField] private int maxSpawnAttempts = 20;

    [Header("=== VISUAL DEBUGGING ===")]
    [Tooltip("Color of spawn radius gizmo in editor")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0f, 0f, 0.2f);

    [Tooltip("Show spawn radius in Scene view")]
    [SerializeField] private bool showSpawnRadius = true;

    [Header("=== WAVE SETTINGS ===")]
    [Tooltip("Number of zombies to spawn per wave")]
    [SerializeField] private int zombiesPerWave = 2;

    [Tooltip("Delay between waves (seconds)")]
    [SerializeField] private float waveDelay = 8f;

    [Tooltip("Should waves continue indefinitely?")]
    [SerializeField] private bool infiniteWaves = true;

    // ========================================================================
    // PRIVATE FIELDS
    // ========================================================================

    private List<GameObject> activeZombies = new List<GameObject>();  // Track all spawned zombies
    private Player player;                                            // Reference to player
    private AudioSource audioSource;                                  // For spawn sounds (optional)
    private int currentWave = 0;                                      // Current wave number
    private bool playerReady = false;                                 // Is player loaded and ready?

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    /// <summary>
    /// Starts the spawn sequence by waiting for player to be ready.
    /// </summary>
    void Start()
    {
        StartCoroutine(WaitForPlayer());
    }

    // ========================================================================
    // INITIALIZATION - PLAYER DETECTION
    // ========================================================================

    /// <summary>
    /// Waits for the player to be loaded before starting spawns.
    /// Prevents zombies from spawning before player exists.
    /// </summary>
    private IEnumerator WaitForPlayer()
    {
        Debug.Log("ZombieSpawner: Waiting for player...");

        float timeout = 5f;  // Don't wait forever
        float timer = 0f;

        while (timer < timeout)
        {
            Player tempPlayer = GameObject.FindAnyObjectByType<Player>();
            if (tempPlayer != null)
            {
                player = tempPlayer;
                playerReady = true;
                //Debug.Log($"ZombieSpawner: Player found after {timer:F1} seconds");
                break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        if (!playerReady)
        {
            Debug.LogError("ZombieSpawner: Player not found after timeout!");
            yield break;
        }

        // Setup audio source for spawn effects (optional)
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Start spawning if enabled
        if (spawnOnStart)
        {
            StartCoroutine(SpawnInitialZombies());
            StartCoroutine(WaveSpawner());
        }
    }

    // ========================================================================
    // INITIAL SPAWNING
    // ========================================================================

    /// <summary>
    /// Spawns the first wave of zombies immediately after player is ready.
    /// </summary>
    private IEnumerator SpawnInitialZombies()
    {
        yield return new WaitForSeconds(0.5f);

        for (int i = 0; i < zombiesPerWave; i++)
        {
            SpawnZombie();
            yield return new WaitForSeconds(0.3f);  // Stagger spawns
        }

        //Debug.Log($"Spawner {gameObject.name} - Initial zombies spawned");
    }

    // ========================================================================
    // WAVE MANAGEMENT
    // ========================================================================

    /// <summary>
    /// Main wave spawner coroutine.
    /// Continuously spawns waves based on configured delay.
    /// </summary>
    private IEnumerator WaveSpawner()
    {
        while (infiniteWaves)
        {
            yield return new WaitForSeconds(waveDelay);

            // Clean up destroyed zombies from list
            activeZombies.RemoveAll(z => z == null);

            // Check if we can spawn more zombies
            if (activeZombies.Count < maxZombies)
            {
                int toSpawn = Mathf.Min(zombiesPerWave, maxZombies - activeZombies.Count);

                if (toSpawn > 0)
                {
                    currentWave++;
                    Debug.Log($"WAVE {currentWave} - Spawning {toSpawn} zombies");

                    for (int i = 0; i < toSpawn; i++)
                    {
                        SpawnZombie();
                        yield return new WaitForSeconds(0.3f);  // Stagger spawns
                    }
                }
            }
        }
    }

    // ========================================================================
    // ZOMBIE SPAWNING LOGIC
    // ========================================================================

    /// <summary>
    /// Spawns a single zombie at a safe position.
    /// Handles all setup: NavMeshAgent, AI brain, Guard component.
    /// </summary>
    public void SpawnZombie()
    {
        // Validate player is ready
        if (!playerReady || player == null)
        {
            //Debug.LogWarning("ZombieSpawner: Player not ready, skipping spawn");
            return;
        }

        // Validate prefabs
        if (zombiePrefab == null)
        {
            Debug.LogError("Zombie prefab not assigned in ZombieSpawner!");
            return;
        }

        if (zombieBrainPrefab == null)
        {
            Debug.LogError("Zombie brain prefab not assigned in ZombieSpawner!");
            return;
        }

        // Find a safe spawn position
        Vector3 spawnPosition = GetSafeSpawnPosition();

        if (spawnPosition == Vector3.zero)
        {
            //Debug.LogWarning("Could not find safe spawn position for zombie!");
            return;
        }

        // Double-check distance from player (safety verification)
        float distanceToPlayer = Vector3.Distance(spawnPosition, player.transform.position);
        if (distanceToPlayer < minDistanceFromPlayer)
        {
            //Debug.LogWarning($"Spawn position too close to player ({distanceToPlayer:F1}m). Retrying...");
            spawnPosition = GetSafeSpawnPosition();
            distanceToPlayer = Vector3.Distance(spawnPosition, player.transform.position);
            if (distanceToPlayer < minDistanceFromPlayer)
            {
                Debug.LogWarning($"Still too close - skipping spawn this wave");
                return;
            }
        }

        //Debug.Log($"Spawning zombie at {spawnPosition}, distance from player: {distanceToPlayer:F1}m");

        // Create zombie instance
        GameObject zombie = Instantiate(zombiePrefab, spawnPosition, Quaternion.identity);
        zombie.name = $"Zombie_{activeZombies.Count + 1}";

        // Setup NavMeshAgent
        NavMeshAgent agent = zombie.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = zombie.AddComponent<NavMeshAgent>();

        agent.enabled = true;
        agent.Warp(spawnPosition);  // Ensures agent is properly positioned on NavMesh

        // Attach AI brain
        AIBrain brain = Instantiate(zombieBrainPrefab, zombie.transform);
        brain.SetAgent(agent);
        // Note: Player is set automatically by AIBrain's auto-find system

        // Setup Guard component (required for AI brain)
        Guard guard = zombie.GetComponent<Guard>();
        if (guard != null)
        {
            guard.currentBrain = brain;
            brain.Init(guard);  // Initialize with guard reference
        }
        else
        {
            Debug.LogError($"Zombie prefab is missing Guard component!");
        }

        // Track active zombie
        activeZombies.Add(zombie);

        // Add cleanup tracker to notify spawner when zombie dies
        ZombieCleanup cleanup = zombie.AddComponent<ZombieCleanup>();
        cleanup.SetSpawner(this);

        //Debug.Log($"Zombie spawned successfully at distance {distanceToPlayer:F1}m from player");
    }

    // ========================================================================
    // SPAWN POSITION CALCULATION
    // ========================================================================

    /// <summary>
    /// Finds a safe position to spawn a zombie.
    /// Checks:
    /// - Position is within spawn radius
    /// - Position is on NavMesh
    /// - Position is not too close to player
    /// </summary>
    /// <returns>Safe spawn position, or Vector3.zero if none found</returns>
    private Vector3 GetSafeSpawnPosition()
    {
        if (!playerReady || player == null)
            return Vector3.zero;

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // Random position within spawn radius
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 testPosition = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

            // Check if position is on NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(testPosition, out hit, 10f, NavMesh.AllAreas))
            {
                float distanceToPlayer = Vector3.Distance(hit.position, player.transform.position);
                if (distanceToPlayer >= minDistanceFromPlayer)
                {
                    return hit.position;  // Valid spawn position found
                }
            }
        }

        // Fallback: use spawner's position if safe from player
        if (Vector3.Distance(transform.position, player.transform.position) >= minDistanceFromPlayer)
        {
            return transform.position;
        }

        // No safe position found
        return Vector3.zero;
    }

    // ========================================================================
    // ZOMBIE TRACKING
    // ========================================================================

    /// <summary>
    /// Called when a zombie is destroyed.
    /// Removes zombie from active list for wave management.
    /// </summary>
    public void ZombieDestroyed(GameObject zombie)
    {
        activeZombies.Remove(zombie);
        //Debug.Log($"Zombie destroyed - Remaining: {activeZombies.Count}");
    }

    /// <summary>
    /// Gets the current number of active zombies.
    /// Automatically cleans up null references.
    /// </summary>
    public int GetActiveZombieCount()
    {
        activeZombies.RemoveAll(z => z == null);
        return activeZombies.Count;
    }

    // ========================================================================
    // EDITOR VISUALIZATION
    // ========================================================================

    /// <summary>
    /// Draws spawn radius gizmo in editor.
    /// Shows where zombies can potentially spawn.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showSpawnRadius) return;

        // Spawn radius (where zombies can appear)
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        // Spawner position marker
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, transform.localScale);

#if UNITY_EDITOR
        // Display spawner info in Scene view
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2,
            $"Zombie Spawner\nRadius: {spawnRadius:F1}\nSafe Distance: {minDistanceFromPlayer:F1}");
#endif
    }

    /// <summary>
    /// Draws additional gizmos when spawner is selected.
    /// Shows minimum distance from player (safety zone).
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, minDistanceFromPlayer);
    }
}

// ============================================================================
// ZOMBIE CLEANUP - HELPER COMPONENT
// ============================================================================
// 
// This component attaches to each spawned zombie and notifies the spawner
// when the zombie is destroyed. This allows the spawner to track active
// zombies and manage waves correctly.
//
// ============================================================================

/// <summary>
/// Helper component attached to each spawned zombie.
/// Notifies the spawner when the zombie is destroyed.
/// </summary>
public class ZombieCleanup : MonoBehaviour
{
    private ZombieSpawner spawner;  // Reference to parent spawner

    /// <summary>
    /// Sets the spawner reference for cleanup notifications.
    /// </summary>
    public void SetSpawner(ZombieSpawner spawner)
    {
        this.spawner = spawner;
    }

    /// <summary>
    /// Called when zombie is destroyed.
    /// Notifies spawner to remove from active list.
    /// </summary>
    private void OnDestroy()
    {
        if (spawner != null)
            spawner.ZombieDestroyed(gameObject);
    }
}