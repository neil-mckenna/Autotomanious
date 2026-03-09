using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;

public class SceneSpawner : MonoBehaviour
{
    [Header("Guard Prefabs")]
    [SerializeField] private GameObject guardPrefab;  
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
    private GameObject currentPlayer;

    private void Start()
    {
        StartCoroutine(SpawnSequence());

    }

    private IEnumerator SpawnSequence()
    {

        //Debug.Log("=== Starting Spawn Sequence ===");

        if (guardsParent == null)
        {
            GameObject parent = new GameObject("AllGuards");
            guardsParent = parent.transform;
            //Debug.Log("Created 'AllGuards' parent object");
        }

        SpawnPlayer();

        yield return null;

        if (mainCamera != null && currentPlayer != null)
        {
            FollowCamera followCamera = mainCamera.GetComponent<FollowCamera>();

            if (followCamera != null)
            {
                followCamera.target = currentPlayer.transform;
                //Debug.Log("Camera following Player");
            }
            else
            {
                //Debug.LogError("Camera doesnt have FollowCamera component");
            }
        }

        SpawnAllGuards();

        //Debug.Log($"=== Spawn Comeplete: {currentGuards.Count} guards, player at {currentPlayer.transform.position} ===");

    }

    // spawn player method on player spawn point
    private void SpawnPlayer()
    {
        if (playerPrefab == null) 
        {
            //Debug.LogError("Player prefab not assigned");
            return;
        }

        if (playerSpawnPoint == null)
        {
            //Debug.LogError("Player spawn point not assigned!");
            return;
        }

        currentPlayer = Instantiate(playerPrefab, playerSpawnPoint.position, playerSpawnPoint.rotation);
        currentPlayer.name = "Player";

        //Debug.Log($"Player Spawned at {playerSpawnPoint.position}");

    }

    // spawn guards amount on 
    private void SpawnAllGuards()
    {
        // saftey checks
        if (guardPrefab == null)
        {
            //Debug.LogError("Guard prefab not assigned!");
            return;
        }

        if (guardSpawnPoints == null || guardSpawnPoints.Length == 0)
        {
            //Debug.LogError("No guard spawn points assigned!");
            return;
        }

        if (allWaypoints == null || allWaypoints.Length == 0)
        {
            //Debug.LogError("No Waypoints assigned!");
            return;
        }

        // initialize the enum
        AISettings.AIType selectedAI = AISettings.Instance.selectedAIType;

        // teritary to match the correct prefab
        AIBrain brainPrefab = selectedAI == AISettings.AIType.FSM ? fsmBrainPrefab : behaviourTreeBrainPrefab;

        // spawn no of guards
        for (int i = 0; i < numberOfGuards; i++)
        {
            SpawnSingleGuard(brainPrefab, i);
        }
    }

    // spawn guard with name, at a random spawn point, attach to parent prefab, get navmesh component, warp onto navmesh 
    private void SpawnSingleGuard(AIBrain brainPrefab, int guardIndex)
    {
        // get a random point
        Transform spawnPoint = guardSpawnPoints[Random.Range(0, guardSpawnPoints.Length)];

        // instantate
        GameObject guardGO = Instantiate(guardPrefab, spawnPoint.position, spawnPoint.rotation, guardsParent);

        guardGO.name = $"Guard_{guardIndex}";

        NavMeshAgent agent  = guardGO.GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            agent.Warp(spawnPoint.position);
            //Debug.Log($"Guard {guardIndex} warped to {spawnPoint.position}");

        }

        AIBrain brain = Instantiate(brainPrefab, guardGO.transform);
        brain.SetAgent(agent);

        Transform[] randomWaypoints = GetRandomWaypointsForGuard(guardIndex);

        // cast
        if (brain is FSM fsmBrain)
        {
            
            fsmBrain.waypoints = randomWaypoints;

            //Debug.Log($"Guard {guardIndex} (FSM) got {randomWaypoints.Length} random waypoints");
        }
        else if(brain is BTBrain btBrain)
        {
            btBrain.SetWaypoints(randomWaypoints);

            //Debug.Log($"Guard {guardIndex} (BT) got {randomWaypoints.Length} waypoints");

        }
        else
        {
            //Debug.Log($"Guard {guardIndex} use BT brain or something else");
        }

        Guard guard = guardGO.GetComponent<Guard>();
        if (guard != null)
        {
            if(currentPlayer != null)
            {
                guard.SetPlayer(currentPlayer.transform);

            }

            guard.currentBrain = brain;
            brain.Init(guard);
        }

        currentGuards.Add(guardGO);
        //Debug.Log($"Guard {guardIndex} fully initialized");
    }

    // just a helper to get a random location for realism and variaialibiltiy
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

    // a button for respawn in editor, but not working
    [ContextMenu("Respawn All Guards")]
    public void RespawnAllGuards()
    {
        foreach (var guard in currentGuards)
        {
            if(guard != null)
            {
                Destroy(guard.gameObject);
            }
        }
        currentGuards.Clear();

        SpawnAllGuards();
    }

    // a button for respawn in editor, but not working
    [ContextMenu("Respawn Player")]
    public void RespawnGuard()
    {
        if(currentPlayer != null)
        {
            Destroy(currentPlayer);
        }

        SpawnPlayer();

        // re-attach camera 
        if(mainCamera !=  null && currentPlayer != null)
        {
            FollowCamera followCamera = mainCamera.GetComponent<FollowCamera>();

            followCamera.target = currentPlayer.transform;
        }
    }

   
}