using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ZombieSpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    [SerializeField] private GameObject zombieParent;
    [SerializeField] private GameObject zombiePrefab;
    [SerializeField] private AIBrain zombieBrainPrefab;
    [SerializeField] private int maxZombies = 10;
    [SerializeField] private float spawnRadius = 8f;
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private bool spawnOnStart = true;

    [Header("Wave Settings")]
    [SerializeField] private int zombiesPerWave = 2;
    [SerializeField] private float waveDelay = 8f;
    [SerializeField] private bool infiniteWaves = true;

    [Header("Visual")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0f, 0f, 0.2f);
    [SerializeField] private bool showSpawnRadius = true;

    [Header("Effects")]
    [SerializeField] private ParticleSystem spawnEffect;
    [SerializeField] private AudioClip spawnSound;

    private List<GameObject> activeZombies = new List<GameObject>();
    private Transform playerTransform;
    private AudioSource audioSource;
    private int currentWave = 0;

    private bool playerReady = false;


    void Start()
    {
        StartCoroutine(WaitForPlayer());
    }

    private IEnumerator WaitForPlayer()
    {
        Debug.Log("ZombieSpawner: Waiting for player...");

        // Wait up to 5 seconds for player
        float timeout = 5f;
        float timer = 0f;

        while (timer < timeout)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerReady = true;
                Debug.Log($"ZombieSpawner: Player found after {timer:F1} seconds");
                break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        if (!playerReady)
        {
            Debug.LogError("ZombieSpawner: Player not found after 5 seconds!");
            yield break;
        }

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && spawnSound != null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Setup parent if needed
        if (zombieParent == null)
        {
            zombieParent = new GameObject("Zombies");
        }

        // Start spawning
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

        Debug.Log($"Spawner {gameObject.name} - Initial zombies spawned");
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
                    Debug.Log($"WAVE {currentWave} - Spawning {toSpawn} zombies");

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
        // Check all dependencies
        if (zombiePrefab == null)
        {
            Debug.LogError("Zombie prefab not assigned!");
            return;
        }

        if (zombieBrainPrefab == null)
        {
            Debug.LogError("Zombie brain prefab not assigned!");
            return;
        }

        if (playerTransform == null)
        {
            Debug.LogError("Player reference missing!");
            return;
        }

        Vector3 spawnPosition = GetRandomSpawnPosition();
        if (spawnPosition == Vector3.zero)
        {
            Debug.LogError("Could not find valid spawn position!");
            return;
        }

        // Play effects
        if (spawnEffect != null)
        {
            Instantiate(spawnEffect, spawnPosition, Quaternion.identity);
        }

        if (spawnSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(spawnSound);
        }

        // Create zombie
        GameObject zombie = Instantiate(zombiePrefab, spawnPosition, Quaternion.identity);
        if (zombieParent != null)
            zombie.transform.SetParent(zombieParent.transform);

        zombie.name = $"Zombie_{activeZombies.Count + 1}";

        // Get OR add NavMeshAgent correctly
        NavMeshAgent agent = zombie.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = zombie.AddComponent<NavMeshAgent>();
        }

        // Enable agent
        agent.enabled = true;

        // Find and warp to NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(spawnPosition, out hit, 25f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            Debug.Log($"Zombie placed on NavMesh at {hit.position}");
        }
        else
        {
            Debug.LogError($"No NavMesh found near {spawnPosition}");
            Destroy(zombie);
            return;
        }

        // Add zombie brain
        AIBrain brain = Instantiate(zombieBrainPrefab, zombie.transform);
        if (brain == null)
        {
            Debug.LogError("Failed to instantiate zombie brain!");
            Destroy(zombie);
            return;
        }

        brain.SetAgent(agent);
        brain.SetPlayer(playerTransform);

        // Setup Guard component
        Guard guard = zombie.GetComponent<Guard>();
        if (guard != null)
        {
            guard.currentBrain = brain;
            brain.Init(guard);
            Debug.Log($"Zombie initialized with player reference");
        }
        else
        {
            Debug.LogError("Guard component missing on zombie prefab!");
            Destroy(zombie);
            return;
        }

        activeZombies.Add(zombie);

        // Add cleanup tracker
        ZombieCleanup cleanup = zombie.AddComponent<ZombieCleanup>();
        cleanup.SetSpawner(this);

        Debug.Log($"Zombie spawned successfully");
    }

    private Vector3 GetRandomSpawnPosition()
    {
        for (int i = 0; i < 20; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPosition = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(spawnPosition, out hit, 10f, NavMesh.AllAreas))
            {
                // Check not too close to player
                if (playerTransform != null && Vector3.Distance(hit.position, playerTransform.position) < 3f)
                {
                    continue;
                }
                return hit.position;
            }
        }
        return Vector3.zero;
    }

    public void ZombieDestroyed(GameObject zombie)
    {
        activeZombies.Remove(zombie);
        Debug.Log($"Zombie destroyed - Remaining: {activeZombies.Count}");
    }

    public int GetActiveZombieCount()
    {
        activeZombies.RemoveAll(z => z == null);
        return activeZombies.Count;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showSpawnRadius) return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
}

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