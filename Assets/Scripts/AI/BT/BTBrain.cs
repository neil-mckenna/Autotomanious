using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// teh Behavior tree AI brain class
public class BTBrain : AIBrain
{

    #region Properties

    [Header("Behaviour Tree Settings")]
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float fieldOfView = 60f;
    [SerializeField] private float chaseSpeed = 5f;
    [SerializeField] private float wanderSpeed = 3.5f;
    [SerializeField] private float searchDuration = 3f;

    [Header("Waypoints")]
    [SerializeField] public Transform[] waypoints;

    private BehaviourTree tree;
    private bool hasInitialised = false;

    // State tracking
    private bool wasPlayerVisible = false;
    private Vector3 lastKnownPlayerPosition;
    private float searchTimer;
    private string currentAction = "Initializing...";

    #endregion

    #region Set Waypoints
    public void SetWaypoints(Transform[] newWaypoints)
    {
        waypoints = newWaypoints;
    }

    #endregion

    #region Init

    public override void Init(Guard guard)
    {
        Debug.Log($"=== BTBrain.Init START for {guard.name} ===");

        this.guard = guard;

        // Ensure we have the agent
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = GetComponentInParent<NavMeshAgent>();
            }
        }

        // Set up agent
        if (agent != null)
        {
            agent.speed = wanderSpeed;
            agent.isStopped = false;
        }

        // Log waypoints
        if (waypoints != null && waypoints.Length > 0)
        {
            currentWaypointIndex = 0;
            Debug.Log($"BTBrain received {waypoints.Length} waypoints");

            // Set first destination
            if (agent != null && waypoints[0] != null)
            {
                agent.SetDestination(waypoints[0].position);
            }
        }
        else
        {
            Debug.LogWarning("BTBrain has NO waypoints!");
        }

        // Build the behaviour tree
        ConstructBehaviourTree();

        hasInitialised = true;
        currentAction = "Patrolling";
        Debug.Log($"=== BTBrain.Init COMPLETE for {guard.name} ===");
    }

    #endregion

    #region Think

    // This is called every frame by Guard.Update()
    public override void Think()
    {
        if (!hasInitialised || tree == null || guard == null)
            return;

        // Process the tree every frame
        Node.Status status = tree.Process();

        // Remove Handles.Label from here - it causes errors!
        // DebugDraw() is now called from OnDrawGizmos only
    }

    #endregion

    #region Seeing Player

    // Condition methods for the tree (return SUCCESS/FAILURE)
    private Node.Status CanSeePlayerCondition()
    {
        if (guard == null || guard.PlayerTransform == null)
            return Node.Status.FAILURE;

        bool canSee = guard.HasLineOfSightToPlayer();

        if (canSee)
        {
            lastKnownPlayerPosition = guard.PlayerTransform.position;
            currentAction = "Chasing";

            if (!wasPlayerVisible)
            {
                wasPlayerVisible = true;
                if (GameManager.Instance != null)
                {
                    Debug.Log("Calling GuardAlerted from BTBrain");
                    GameManager.Instance.GuardAlerted();  // Only call once per encounter
                }

            }


            if (GameManager.Instance != null)
            {

                GameManager.Instance.PlayerSpotted(); // Increments times spotted
            }

            return Node.Status.SUCCESS;
        }
        else
        {
            if (wasPlayerVisible)
            {
                wasPlayerVisible = false;
                if (GameManager.Instance != null)
                    GameManager.Instance.GuardLostPlayer(); // Alert ends
            }
        }

        return Node.Status.FAILURE;
    }

    private Node.Status LostPlayerCondition()
    {
        if (lastKnownPlayerPosition != Vector3.zero && searchTimer > 0)
        {
            currentAction = "Searching";
            return Node.Status.SUCCESS;
        }
        return Node.Status.FAILURE;
    }

    #endregion

    #region ChasePlayer

    // Called by ChasePlayerAction when we need to chase
    public override void Chase(Transform target)
    {
        if (target != null && agent != null)
        {
            agent.speed = chaseSpeed;
            Seek(target.position);
        }
    }

    // Action methods for the tree (can return RUNNING)
    private Node.Status ChasePlayerAction()
    {
        if (guard == null || guard.PlayerTransform == null)
            return Node.Status.FAILURE;

        // Use the Chase method we overrode
        Chase(guard.PlayerTransform);

        lastKnownPlayerPosition = guard.PlayerTransform.position;

        if (!guard.HasLineOfSightToPlayer())
        {
            searchTimer = searchDuration;
            return Node.Status.SUCCESS; // Lost player, move to search
        }

        return Node.Status.RUNNING;
    }

    #endregion

    #region Searching

    private Node.Status SearchAction()
    {
        if (lastKnownPlayerPosition == Vector3.zero || agent == null)
            return Node.Status.FAILURE;

        searchTimer -= Time.deltaTime;

        // If we see the player while searching, chase them
        if (guard != null && guard.HasLineOfSightToPlayer())
        {
            searchTimer = 0;
            return Node.Status.SUCCESS; // Found player, go to chase
        }

        // Move to search position
        if (!agent.hasPath || agent.remainingDistance < 0.5f)
        {
            Vector3 searchPosition = lastKnownPlayerPosition + Random.insideUnitSphere * 5f;
            searchPosition.y = lastKnownPlayerPosition.y;
            Seek(searchPosition);
        }

        // Check if search time is up
        if (searchTimer <= 0)
        {
            lastKnownPlayerPosition = Vector3.zero;
            currentAction = "Patrolling";
            return Node.Status.SUCCESS; // Search done, go to patrol
        }

        return Node.Status.RUNNING;
    }

    #endregion

    #region Patrolling

    // Called by PatrolAction when we need to wander
    public override void Wander()
    {
        if (agent == null) return;

        agent.speed = wanderSpeed;

        if (!agent.hasPath || agent.remainingDistance < 0.5f)
        {
            if (waypoints != null && waypoints.Length > 0)
            {
                Transform targetWaypoint = waypoints[currentWaypointIndex];
                if (targetWaypoint != null)
                {
                    Seek(targetWaypoint.position);
                    currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                }
            }
            else
            {
                // Random wandering
                Vector3 randomDirection = Random.insideUnitSphere * 10f;
                randomDirection += transform.position;

                if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, 10, NavMesh.AllAreas))
                {
                    Seek(hit.position);
                }
            }
        }
    }

    private Node.Status PatrolAction()
    {
        // Use the Wander method we overrode
        Wander();

        // If we see the player while patrolling, chase them
        if (guard != null && guard.HasLineOfSightToPlayer())
        {
            currentAction = "Chasing";
            return Node.Status.FAILURE; // Failure triggers selector to try chase
        }

        return Node.Status.RUNNING;
    }

    #endregion

    #region Behavior Tree Code

    private void ConstructBehaviourTree()
    {
        // create a tree base or stump
        tree = new BehaviourTree("BTBrain Tree");

        // Root selector or main tree
        Selector root = new Selector("Root Selector");

        //-------------------------------
        // 1. CHASE BEHAVIOR (highest priority)
        Sequence chaseSequence = new Sequence("Chase Sequence");

        Leaf canSeePlayer = new Leaf("Can See Player?", CanSeePlayerCondition);
        Leaf chasePlayer = new Leaf("Chase Player", ChasePlayerAction);

        // add leafs to chase sequence
        chaseSequence.AddChild(canSeePlayer);
        chaseSequence.AddChild(chasePlayer);

        //-------------------------
        // 2. SEARCH BEHAVIOUR (medium priority)
        Sequence searchSequence = new Sequence("Search Sequence");

        // leaf depending on the methods
        Leaf lostPlayer = new Leaf("Lost Player?", LostPlayerCondition);
        Leaf searchAtLastPosition = new Leaf("Search at Last Position", SearchAction);

        // add child to searchSequence
        searchSequence.AddChild(lostPlayer);
        searchSequence.AddChild(searchAtLastPosition);


        //--------------------------------------
        // 3. PATROL BEHAVIOR (lowest priority)
        Sequence patrolSequence = new Sequence("Patrol Sequence");

        Leaf patrol = new Leaf("Patrol", PatrolAction);

        // just one leaf child
        patrolSequence.AddChild(patrol);


        //--------------------------------
        root.AddChild(chaseSequence);
        root.AddChild(searchSequence);
        root.AddChild(patrolSequence);

        tree.AddChild(root);

        // Print tree structure
        tree.PrintTree();
    }

    #endregion

    #region Debug

    // This is called by Unity for drawing gizmos in the Scene view
    private void OnDrawGizmos()
    {
        // Only draw if in play mode and initialized
        if (!Application.isPlaying || !hasInitialised || agent == null)
            return;

        // Draw current action above guard
        Vector3 textPos = transform.position + Vector3.up * 2.5f;

#if UNITY_EDITOR
        // Use Handles for text - this is safe in OnDrawGizmos
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(textPos, $"BT: {currentAction}");

        if (agent.hasPath && agent.destination != Vector3.zero)
        {
            UnityEditor.Handles.Label(textPos + Vector3.down * 0.5f, $"Dest: {agent.destination}");
        }
#endif

        // Draw line to destination (Debug.DrawLine works anywhere)
        if (agent.hasPath && agent.destination != Vector3.zero)
        {
            Debug.DrawLine(transform.position, agent.destination, Color.cyan);
        }

        // Draw waypoints
        if (waypoints != null)
        {
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] != null)
                {
                    // Draw waypoint marker
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawSphere(waypoints[i].position, 0.3f);

                    // Draw lines between waypoints
                    int next = (i + 1) % waypoints.Length;
                    if (waypoints[next] != null)
                    {
                        Debug.DrawLine(waypoints[i].position, waypoints[next].position, Color.cyan);
                    }
                }
            }
        }

        // Draw last known player position if searching
        if (currentAction == "Searching" && lastKnownPlayerPosition != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lastKnownPlayerPosition, 1f);
            Debug.DrawLine(transform.position, lastKnownPlayerPosition, Color.yellow);
        }

        // Draw search timer
        if (searchTimer > 0)
        {
            Vector3 timerPos = transform.position + Vector3.up * 2f;
#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.Label(timerPos, $"Search: {searchTimer:F1}s");
#endif
        }
    }

    #endregion


}