using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// ============================================================================
// SCENE SPAWNER - HANDLES INITIAL SPAWNING OF PLAYER AND GUARDS
// ============================================================================
// 
// This script is responsible for:
// 1. Spawning the player at a designated spawn point
// 2. Spawning guards based on the selected AI type (FSM, Behavior Tree, or Zombie)
// 3. Assigning random waypoints to each guard for patrolling
// 4. Organizing spawned objects in the hierarchy
// 5. Providing respawn functionality via context menu
//
// HOW TO USE:
// - Attach to an empty GameObject in your scene
// - Assign all required prefabs and references in the Inspector
// - Configure spawn points and waypoints
// - Set the desired AI type in AISettings
// - The spawner will automatically spawn everything on Start
//
// ============================================================================

public class SceneSpawner : MonoBehaviour
{
    // ========================================================================
    // SERIALIZED FIELDS - CONFIGURE IN UNITY INSPECTOR
    // ========================================================================

    [Header("=== GUARD PREFABS ===")]
    [Tooltip("The base guard GameObject prefab (contains Guard component, visuals, etc.)")]
    [SerializeField] private GameObject guardPrefab;

    [Tooltip("Prefab containing the Zombie AI brain (attaches to guard)")]
    [SerializeField] private AIBrain zombieBrainPrefab;

    [Tooltip("Prefab containing the FSM AI brain (attaches to guard)")]
    [SerializeField] private AIBrain fsmBrainPrefab;

    [Tooltip("Prefab containing the Behavior Tree AI brain (attaches to guard)")]
    [SerializeField] private AIBrain behaviourTreeBrainPrefab;

    [Header("=== SPAWN POINTS ===")]
    [Tooltip("Number of guards to spawn in the scene")]
    [SerializeField] private int numberOfGuards = 3;

    [Tooltip("Array of possible spawn locations for guards (randomly selected)")]
    [SerializeField] private Transform[] guardSpawnPoints;

    [Tooltip("Fixed spawn location for the player")]
    [SerializeField] private Transform playerSpawnPoint;

    [Header("=== PLAYER PREFAB ===")]
    [Tooltip("The player GameObject prefab (contains Drive, Player components)")]
    [SerializeField] private GameObject playerPrefab;

    [Header("=== WAYPOINTS ===")]
    [Tooltip("All available waypoints in the scene for guard patrolling")]
    [SerializeField] private Transform[] allWaypoints;

    [Header("=== CAMERA ===")]
    [Tooltip("Main camera reference (optional, will auto-find if null)")]
    [SerializeField] public Camera mainCamera;

    [Header("=== HIERARCHY ORGANIZATION ===")]
    [Tooltip("Parent transform for all spawned guards (auto-created if null)")]
    [SerializeField] private Transform guardsParent;

    // ========================================================================
    // PRIVATE FIELDS
    // ========================================================================

    private List<GameObject> currentGuards = new List<GameObject>();  // Track all spawned guards
    private Player currentPlayer;                                     // Reference to spawned player
    private GameObject currentPlayerGO;                               // Player GameObject reference

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    private void Start()
    {
        // Start spawn sequence as coroutine to ensure proper timing
        StartCoroutine(SpawnSequence());
    }

    // ========================================================================
    // SPAWN SEQUENCE
    // ========================================================================

    /// <summary>
    /// Main spawn sequence coroutine.
    /// Spawns player first, waits for stabilization, then spawns guards.
    /// </summary>
    private IEnumerator SpawnSequence()
    {
        // Create parent container for guards if not assigned
        if (guardsParent == null)
        {
            GameObject parent = new GameObject("AllGuards");
            guardsParent = parent.transform;
            Debug.Log("Created 'AllGuards' parent container for guards");
        }

        // Step 1: Spawn player first (guards need player reference)
        SpawnPlayer();

        // Step 2: Wait for player physics to stabilize
        // This prevents guards from detecting player at spawn location incorrectly
        yield return new WaitForSeconds(0.5f);

        // Step 3: Spawn all guards with their AI brains
        SpawnAllGuards();
    }

    // ========================================================================
    // PLAYER SPAWNING
    // ========================================================================

    /// <summary>
    /// Spawns the player at the designated spawn point.
    /// Handles position forcing, movement reset, and ground snapping.
    /// </summary>
    private void SpawnPlayer()
    {
        // Validate prefab
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab not assigned in SceneSpawner!");
            return;
        }

        // Validate spawn point
        if (playerSpawnPoint == null)
        {
            Debug.LogError("Player spawn point not assigned in SceneSpawner!");
            return;
        }

        // Instantiate player at spawn position
        currentPlayerGO = Instantiate(playerPrefab, playerSpawnPoint.position, playerSpawnPoint.rotation);
        currentPlayerGO.name = "Player";
        currentPlayer = currentPlayerGO.GetComponent<Player>();

        // Force position to spawn point (ensures exact placement)
        currentPlayerGO.transform.position = playerSpawnPoint.position;
        currentPlayerGO.transform.rotation = playerSpawnPoint.rotation;

        // Reset any lingering movement from previous spawns
        Drive drive = currentPlayerGO.GetComponent<Drive>();
        if (drive != null)
        {
            drive.ResetMovement();
        }

        // Snap player to ground to prevent floating
        StartCoroutine(SnapToGround());

        Debug.Log($"Player spawned successfully at {playerSpawnPoint.position}");
    }

    /// <summary>
    /// Snaps the player to the ground using raycast.
    /// Prevents player from floating above or falling through terrain.
    /// </summary>
    private IEnumerator SnapToGround()
    {
        // Wait one frame for physics to initialize
        yield return null;

        if (currentPlayerGO != null)
        {
            RaycastHit hit;
            if (Physics.Raycast(currentPlayerGO.transform.position, Vector3.down, out hit, 10f))
            {
                Vector3 newPos = currentPlayerGO.transform.position;
                newPos.y = hit.point.y + 0.1f;  // Small offset to prevent ground clipping
                currentPlayerGO.transform.position = newPos;
                Debug.Log($"Player snapped to ground at Y={newPos.y}");
            }
            else
            {
                Debug.LogWarning("Could not snap player to ground - no ground detected below spawn point");
            }
        }
    }

    // ========================================================================
    // GUARD SPAWNING
    // ========================================================================

    /// <summary>
    /// Spawns all guards based on the selected AI type from AISettings.
    /// Validates all required references before spawning.
    /// </summary>
    private void SpawnAllGuards()
    {
        // Validate guard prefab
        if (guardPrefab == null)
        {
            Debug.LogError("Guard prefab not assigned in SceneSpawner!");
            return;
        }

        // Validate spawn points
        if (guardSpawnPoints == null || guardSpawnPoints.Length == 0)
        {
            Debug.LogError("No guard spawn points assigned in SceneSpawner!");
            return;
        }

        // Validate waypoints
        if (allWaypoints == null || allWaypoints.Length == 0)
        {
            Debug.LogError("No waypoints assigned in SceneSpawner!");
            return;
        }

        // Get selected AI type from global settings
        AISettings.AIType selectedAI = AISettings.Instance.selectedAIType;

        // Select the appropriate brain prefab based on AI type
        AIBrain brainPrefab = null;

        switch (selectedAI)
        {
            case AISettings.AIType.FSM:
                brainPrefab = fsmBrainPrefab;
                Debug.Log($"Spawning guards with FSM (Finite State Machine) AI");
                break;

            case AISettings.AIType.BehaviourTree:
                brainPrefab = behaviourTreeBrainPrefab;
                Debug.Log($"Spawning guards with Behavior Tree AI");
                break;

            case AISettings.AIType.Zombie:
                brainPrefab = zombieBrainPrefab;
                Debug.Log($"Spawning guards with Zombie AI");
                break;

            default:
                brainPrefab = fsmBrainPrefab;
                Debug.Log($"Unknown AI type, defaulting to FSM");
                break;
        }

        // Spawn each guard
        for (int i = 0; i < numberOfGuards; i++)
        {
            SpawnSingleGuard(brainPrefab, i);
        }

        //Debug.Log($"Successfully spawned {numberOfGuards} guards with {selectedAI} AI");
    }

    /// <summary>
    /// Spawns a single guard with the specified AI brain.
    /// Sets up NavMeshAgent, waypoints, and initializes the brain.
    /// </summary>
    /// <param name="brainPrefab">The AI brain prefab to attach</param>
    /// <param name="guardIndex">Index number for naming</param>
    private void SpawnSingleGuard(AIBrain brainPrefab, int guardIndex)
    {
        // Select random spawn point from available points
        Transform spawnPoint = guardSpawnPoints[Random.Range(0, guardSpawnPoints.Length)];

        // Instantiate guard GameObject
        GameObject guardGO = Instantiate(guardPrefab, spawnPoint.position, spawnPoint.rotation, guardsParent);
        guardGO.name = $"Guard_{guardIndex}";

        // Setup NavMeshAgent
        NavMeshAgent agent = guardGO.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            // Warp ensures proper NavMesh positioning
            agent.Warp(spawnPoint.position);
        }
        else
        {
            Debug.LogWarning($"Guard_{guardIndex} has no NavMeshAgent component!");
        }

        // Instantiate and attach AI brain
        AIBrain brain = Instantiate(brainPrefab, guardGO.transform);
        brain.SetAgent(agent);

        // Assign random waypoints for patrol behavior
        Transform[] randomWaypoints = GetRandomWaypointsForGuard(guardIndex);

        // Set waypoints based on brain type (polymorphic assignment)
        if (brain is Zombie zombieBrain)
        {
            zombieBrain.SetWaypoints(randomWaypoints);
        }
        else if (brain is FSM fsmBrain)
        {
            fsmBrain.SetWaypoints(randomWaypoints);
        }
        else if (brain is BTBrain btBrain)
        {
            btBrain.SetWaypoints(randomWaypoints);
        }

        // Initialize the Guard component
        Guard guard = guardGO.GetComponent<Guard>();
        if (guard != null)
        {
            guard.currentBrain = brain;

            // Initialize brain with player reference if available
            if (currentPlayer != null)
            {
                brain.Init(guard, currentPlayer);
                //Debug.Log($"Guard_{guardIndex}: Initialized with player {currentPlayer.name}");
            }
            else
            {
                // Fallback initialization without player (will auto-find)
                brain.Init(guard);
                Debug.LogWarning($"Guard_{guardIndex}: No player found during spawn - will auto-find");
            }
        }
        else
        {
            Debug.LogError($"Guard_{guardIndex} has no Guard component!");
        }

        // Track spawned guard for respawn functionality
        currentGuards.Add(guardGO);
    }

    // ========================================================================
    // WAYPOINT MANAGEMENT
    // ========================================================================

    /// <summary>
    /// Selects a random subset of waypoints for a guard to patrol.
    /// Each guard gets unique waypoints for varied patrol patterns.
    /// </summary>
    /// <param name="guardIndex">Index of the guard (unused but could be used for seed)</param>
    /// <returns>Array of randomly selected waypoints</returns>
    private Transform[] GetRandomWaypointsForGuard(int guardIndex)
    {
        // Define waypoint selection parameters
        int minWaypoints = 3;                                    // Minimum patrol points
        int maxWaypoints = Mathf.Min(6, allWaypoints.Length);    // Maximum (capped by available)
        int waypointCount = Random.Range(minWaypoints, maxWaypoints);

        // Select random waypoints without duplicates
        List<Transform> selectedWaypoints = new List<Transform>();
        List<Transform> availableWaypoints = new List<Transform>(allWaypoints);

        for (int i = 0; i < waypointCount && availableWaypoints.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, availableWaypoints.Count);
            selectedWaypoints.Add(availableWaypoints[randomIndex]);
            availableWaypoints.RemoveAt(randomIndex);  // Remove to prevent duplicates
        }

        //Debug.Log($"Guard_{guardIndex} assigned {selectedWaypoints.Count} waypoints for patrol");
        return selectedWaypoints.ToArray();
    }

    // ========================================================================
    // PUBLIC METHODS - CAN BE CALLED FROM OTHER SCRIPTS
    // ========================================================================

    /// <summary>
    /// Respawns all guards in the scene.
    /// Destroys existing guards and creates new ones.
    /// Can be called from Unity Context Menu or other scripts.
    /// </summary>
    [ContextMenu("Respawn All Guards")]
    public void RespawnAllGuards()
    {
        // Destroy all existing guards
        foreach (var guard in currentGuards)
        {
            if (guard != null)
            {
                Destroy(guard.gameObject);
            }
        }

        // Clear the tracking list
        currentGuards.Clear();

        // Spawn new guards
        SpawnAllGuards();

        //Debug.Log("All guards respawned successfully");
    }

    // ========================================================================
    // HELPER METHODS - GETTERS
    // ========================================================================

    /// <summary>
    /// Gets the current player GameObject.
    /// </summary>
    public GameObject GetPlayerGameObject() => currentPlayerGO;

    /// <summary>
    /// Gets the current player component.
    /// </summary>
    public Player GetPlayer() => currentPlayer;

    /// <summary>
    /// Gets all currently spawned guards.
    /// </summary>
    public List<GameObject> GetCurrentGuards() => currentGuards;
}