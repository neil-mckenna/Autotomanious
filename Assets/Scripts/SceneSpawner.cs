using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;

public class SceneSpawner : MonoBehaviour
{
    [Header("Guard Prefabs")]
    [SerializeField] private GameObject guardPrefab;
    [SerializeField] private AIBrain zombieBrainPrefab;
    [SerializeField] private AIBrain fsmBrainPrefab;
    [SerializeField] private AIBrain behaviourTreeBrainPrefab;

    [Header("Spawn Points")]
    [SerializeField] private int numberOfGuards = 3;
    [SerializeField] private Transform[] guardSpawnPoints;
    [SerializeField] private Transform playerSpawnPoint;

    [Header("Player Prefab")]
    [SerializeField] private GameObject playerPrefab;

    [Header("All Waypoints in Scene")]
    [SerializeField] private Transform[] allWaypoints;

    [Header("Camera")]
    [SerializeField] public Camera mainCamera;

    [Header("Hierarchy Organisation")]
    [SerializeField] private Transform guardsParent;

    private List<GameObject> currentGuards = new List<GameObject>();
    private Player currentPlayer;
    private GameObject currentPlayerGO; // Store the GameObject

    private void Start()
    {
        StartCoroutine(SpawnSequence());
    }

    private IEnumerator SpawnSequence()
    {
        if (guardsParent == null)
        {
            GameObject parent = new GameObject("AllGuards");
            guardsParent = parent.transform;
        }

        //  Spawn player first
        SpawnPlayer();

        //  Wait for player to stabilize
        yield return new WaitForSeconds(0.5f);



        //  Spawn guards
        SpawnAllGuards();
    }

    private void SpawnPlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab not assigned");
            return;
        }

        if (playerSpawnPoint == null)
        {
            Debug.LogError("Player spawn point not assigned!");
            return;
        }

        currentPlayerGO = Instantiate(playerPrefab, playerSpawnPoint.position, playerSpawnPoint.rotation);
        currentPlayerGO.name = "Player";
        currentPlayer = currentPlayerGO.GetComponent<Player>();

        //  Force position to spawn point
        currentPlayerGO.transform.position = playerSpawnPoint.position;
        currentPlayerGO.transform.rotation = playerSpawnPoint.rotation;

        //  Reset any movement
        Drive drive = currentPlayerGO.GetComponent<Drive>();
        if (drive != null)
        {
            drive.ResetMovement();
        }

        // Snap to ground
        StartCoroutine(SnapToGround());

        Debug.Log($"Player spawned at {playerSpawnPoint.position}");
    }

    private IEnumerator SnapToGround()
    {
        yield return null; // Wait one frame

        if (currentPlayerGO != null)
        {
            RaycastHit hit;
            if (Physics.Raycast(currentPlayerGO.transform.position, Vector3.down, out hit, 10f))
            {
                Vector3 newPos = currentPlayerGO.transform.position;
                newPos.y = hit.point.y + 0.1f;
                currentPlayerGO.transform.position = newPos;
                Debug.Log($"Player snapped to ground at Y={newPos.y}");
            }
        }
    }

    private void SpawnAllGuards()
    {
        if (guardPrefab == null)
        {
            Debug.LogError("Guard prefab not assigned!");
            return;
        }

        if (guardSpawnPoints == null || guardSpawnPoints.Length == 0)
        {
            Debug.LogError("No guard spawn points assigned!");
            return;
        }

        if (allWaypoints == null || allWaypoints.Length == 0)
        {
            Debug.LogError("No Waypoints assigned!");
            return;
        }

        AISettings.AIType selectedAI = AISettings.Instance.selectedAIType;

        AIBrain brainPrefab = null;

        switch (selectedAI)
        {
            case AISettings.AIType.FSM:
                brainPrefab = fsmBrainPrefab;
                break;
            case AISettings.AIType.BehaviourTree:
                brainPrefab = behaviourTreeBrainPrefab;
                break;
            case AISettings.AIType.Zombie:
                brainPrefab = zombieBrainPrefab;
                break;
            default:
                brainPrefab = fsmBrainPrefab;
                break;
        }

        for (int i = 0; i < numberOfGuards; i++)
        {
            SpawnSingleGuard(brainPrefab, i);
        }
    }

    private void SpawnSingleGuard(AIBrain brainPrefab, int guardIndex)
    {
        Transform spawnPoint = guardSpawnPoints[Random.Range(0, guardSpawnPoints.Length)];

        GameObject guardGO = Instantiate(guardPrefab, spawnPoint.position, spawnPoint.rotation, guardsParent);
        guardGO.name = $"Guard_{guardIndex}";

        NavMeshAgent agent = guardGO.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.Warp(spawnPoint.position);
        }

        AIBrain brain = Instantiate(brainPrefab, guardGO.transform);
        brain.SetAgent(agent);

        Transform[] randomWaypoints = GetRandomWaypointsForGuard(guardIndex);

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

        Guard guard = guardGO.GetComponent<Guard>();
        if (guard != null)
        {
            guard.currentBrain = brain;

            if (currentPlayer != null)
            {
                // Use the Init with player
                brain.Init(guard, currentPlayer);
                Debug.Log($"Guard {guardIndex}: Initialized with player {currentPlayer.name}");
            }
            else
            {
                brain.Init(guard);
                Debug.LogWarning($"Guard {guardIndex}: No player found!");
            }
        }

        currentGuards.Add(guardGO);
    }

    private Transform[] GetRandomWaypointsForGuard(int guardIndex)
    {
        int minWaypoints = 3;
        int maxWaypoints = Mathf.Min(6, allWaypoints.Length);
        int waypointCount = Random.Range(minWaypoints, maxWaypoints);

        List<Transform> selectedWaypoints = new List<Transform>();
        List<Transform> availableWaypoints = new List<Transform>(allWaypoints);

        for (int i = 0; i < waypointCount && availableWaypoints.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, availableWaypoints.Count);
            selectedWaypoints.Add(availableWaypoints[randomIndex]);
            availableWaypoints.RemoveAt(randomIndex);
        }

        return selectedWaypoints.ToArray();
    }

    [ContextMenu("Respawn All Guards")]
    public void RespawnAllGuards()
    {
        foreach (var guard in currentGuards)
        {
            if (guard != null)
            {
                Destroy(guard.gameObject);
            }
        }
        currentGuards.Clear();
        SpawnAllGuards();
    }

       
    
}