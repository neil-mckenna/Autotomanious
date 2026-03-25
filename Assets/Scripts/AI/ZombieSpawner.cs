using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ZombieSpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    [SerializeField] private GameObject zombiePrefab;
    [SerializeField] private AIBrain zombieBrainPrefab;
    [SerializeField] private int maxZombies = 10;
    [SerializeField] private float spawnRadius = 8f;
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private bool spawnOnStart = true;

    [Header("Spawn Safety")]
    [SerializeField] private float minDistanceFromPlayer = 5f;
    [SerializeField] private int maxSpawnAttempts = 20;

    [Header("Visual")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0f, 0f, 0.2f);
    [SerializeField] private bool showSpawnRadius = true;

    [Header("Wave Settings")]
    [SerializeField] private int zombiesPerWave = 2;
    [SerializeField] private float waveDelay = 8f;
    [SerializeField] private bool infiniteWaves = true;

    private List<GameObject> activeZombies = new List<GameObject>();
    private Player player;
    private AudioSource audioSource;
    private int currentWave = 0;
    private bool playerReady = false;  //  NEW: Track if player is ready

    void Start()
    {
        //  Wait for player to be ready before spawning
        StartCoroutine(WaitForPlayer());
    }

    private IEnumerator WaitForPlayer()
    {
        Debug.Log(" ZombieSpawner: Waiting for player...");

        float timeout = 5f;
        float timer = 0f;

        while (timer < timeout)
        {
            Player tempPlayer = GameObject.FindAnyObjectByType<Player>();
            if (tempPlayer != null)
            {
                player = tempPlayer;
                playerReady = true;
                Debug.Log($" ZombieSpawner: Player found after {timer:F1} seconds");
                break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        if (!playerReady)
        {
            Debug.LogError(" ZombieSpawner: Player not found after timeout!");
            yield break;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        //  Now start spawning
        if (spawnOnStart)
        {
            StartCoroutine(SpawnInitialZombies());
            StartCoroutine(WaveSpawner());
        }
    }

    private IEnumerator SpawnInitialZombies()
    {
        yield return new WaitForSeconds(0.5f);

        for (int i = 0; i < zombiesPerWave; i++)
        {
            SpawnZombie();
            yield return new WaitForSeconds(0.3f);
        }

        Debug.Log($" Spawner {gameObject.name} - Initial zombies spawned");
    }

    private IEnumerator WaveSpawner()
    {
        while (infiniteWaves)
        {
            yield return new WaitForSeconds(waveDelay);

            activeZombies.RemoveAll(z => z == null);

            if (activeZombies.Count < maxZombies)
            {
                int toSpawn = Mathf.Min(zombiesPerWave, maxZombies - activeZombies.Count);

                if (toSpawn > 0)
                {
                    currentWave++;
                    Debug.Log($" WAVE {currentWave} - Spawning {toSpawn} zombies");

                    for (int i = 0; i < toSpawn; i++)
                    {
                        SpawnZombie();
                        yield return new WaitForSeconds(0.3f);
                    }
                }
            }
        }
    }

    public void SpawnZombie()
    {
        //  Check if player is ready
        if (!playerReady || player == null)
        {
            Debug.LogWarning(" ZombieSpawner: Player not ready, skipping spawn");
            return;
        }

        if (zombiePrefab == null)
        {
            Debug.LogError(" Zombie prefab not assigned!");
            return;
        }

        if (zombieBrainPrefab == null)
        {
            Debug.LogError(" Zombie brain prefab not assigned!");
            return;
        }

        // Find a safe spawn position
        Vector3 spawnPosition = GetSafeSpawnPosition();

        if (spawnPosition == Vector3.zero)
        {
            Debug.LogWarning(" Could not find safe spawn position for zombie!");
            return;
        }

        // Double-check distance from player
        float distanceToPlayer = Vector3.Distance(spawnPosition, player.transform.position);
        if (distanceToPlayer < minDistanceFromPlayer)
        {
            Debug.LogWarning($" Spawn position too close to player ({distanceToPlayer:F1}m). Retrying...");
            spawnPosition = GetSafeSpawnPosition();
            distanceToPlayer = Vector3.Distance(spawnPosition, player.transform.position);
            if (distanceToPlayer < minDistanceFromPlayer)
            {
                Debug.LogWarning($" Still too close - skipping spawn this wave");
                return;
            }
        }

        Debug.Log($" Spawning zombie at {spawnPosition}, distance from player: {distanceToPlayer:F1}m");

        // Create zombie
        GameObject zombie = Instantiate(zombiePrefab, spawnPosition, Quaternion.identity);
        zombie.name = $"Zombie_{activeZombies.Count + 1}";

        // Setup NavMeshAgent
        NavMeshAgent agent = zombie.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = zombie.AddComponent<NavMeshAgent>();

        agent.enabled = true;
        agent.Warp(spawnPosition);

        // Add zombie brain
        AIBrain brain = Instantiate(zombieBrainPrefab, zombie.transform);
        brain.SetAgent(agent);
        //brain.SetPlayer(player);

        // Setup Guard component
        Guard guard = zombie.GetComponent<Guard>();
        if (guard != null)
        {
            guard.currentBrain = brain;
            brain.Init(guard);
        }

        activeZombies.Add(zombie);

        // Add cleanup tracker
        ZombieCleanup cleanup = zombie.AddComponent<ZombieCleanup>();
        cleanup.SetSpawner(this);

        Debug.Log($" Zombie spawned successfully at distance {distanceToPlayer:F1}m from player");
    }

    private Vector3 GetSafeSpawnPosition()
    {
        if (!playerReady || player == null)
            return Vector3.zero;

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 testPosition = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(testPosition, out hit, 10f, NavMesh.AllAreas))
            {
                float distanceToPlayer = Vector3.Distance(hit.position, player.transform.position);
                if (distanceToPlayer >= minDistanceFromPlayer)
                {
                    return hit.position;
                }
            }
        }

        if (Vector3.Distance(transform.position, player.transform.position) >= minDistanceFromPlayer)
        {
            return transform.position;
        }

        return Vector3.zero;
    }

    public void ZombieDestroyed(GameObject zombie)
    {
        activeZombies.Remove(zombie);
        Debug.Log($" Zombie destroyed - Remaining: {activeZombies.Count}");
    }

    public int GetActiveZombieCount()
    {
        activeZombies.RemoveAll(z => z == null);
        return activeZombies.Count;
    }

    private void OnDrawGizmos()
    {
        if (!showSpawnRadius) return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, transform.localScale);

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2,
            $"Zombie Spawner\nRadius: {spawnRadius:F1}\nSafe Distance: {minDistanceFromPlayer:F1}");
#endif
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, minDistanceFromPlayer);
    }
}

// Helper component for cleanup
public class ZombieCleanup : MonoBehaviour
{
    private ZombieSpawner spawner;

    public void SetSpawner(ZombieSpawner spawner)
    {
        this.spawner = spawner;
    }

    private void OnDestroy()
    {
        if (spawner != null)
            spawner.ZombieDestroyed(gameObject);
    }
}