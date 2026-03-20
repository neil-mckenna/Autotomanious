using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// teh Behavior tree AI brain class
public class BTBrain : AIBrain
{

    #region Properties

    [Header("Behaviour Tree Settings")]
    //[SerializeField] private float detectionRange = 10f;
    //[SerializeField] private float fieldOfView = 60f;
    [SerializeField] private float chaseSpeed = 5f;
    [SerializeField] private float wanderSpeed = 3.5f;
    [SerializeField] private float searchDuration = 3f;


    private BehaviourTree tree;
    private bool hasInitialised = false;

    // State tracking
    private bool wasPlayerVisible = false;
    
    private float searchTimer;
    private string currentAction = "Initializing...";

    private bool isSuspicious = false;
    private Vector3 suspiciousPosition;
    private float suspiciousTimer;

    // wait times
    private bool isWaiting = false;
    private float waitTimer = 0f;
    private float minWaitTime = 2f;
    private float maxWaitTime = 10f;

    public string GetCurrentAction()
    {
        return currentAction;
    }

    #endregion

    

    #region Init

    public override void Init(Guard guard)
    {
        //Debug.Log($"=== BTBrain.Init START for {guard.name} ===");

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
            //Debug.Log($"BTBrain received {waypoints.Length} waypoints");

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
        //Debug.Log($"=== BTBrain.Init COMPLETE for {guard.name} ===");
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
        

    }

    #endregion

    #region Hearing Player
    public void SetSuspicious(Vector3 position, float duration)
    {
        isSuspicious = true;
        suspiciousPosition = position;
        suspiciousTimer = duration;
        currentAction = "Suspicious";

        Debug.Log($"BT Guard suspicious at {position} from noise");
    }

    // Add this condition for the tree
    private Node.Status IsSuspiciousCondition()
    {
        if (isSuspicious && suspiciousTimer > 0)
        {
            return Node.Status.SUCCESS;
        }
        return Node.Status.FAILURE;
    }

    // Add this action for the tree
    private Node.Status SuspiciousAction()
    {
        if (!isSuspicious) return Node.Status.FAILURE;

        suspiciousTimer -= Time.deltaTime;
        currentAction = "Suspicious";

        // INVESTIGATE RANDOM AREA
        if (agent != null && (!agent.hasPath || agent.remainingDistance < 1.0f))
        {
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float randomDistance = Random.Range(3f, 8f);

            Vector3 randomOffset = new Vector3(
                Mathf.Cos(randomAngle) * randomDistance,
                0,
                Mathf.Sin(randomAngle) * randomDistance
            );

            Seek(suspiciousPosition + randomOffset);
        }

        // Check if player is in grass FIRST
        bool playerInGrass = IsPlayerInGrass();

        // Only check line of sight if player is NOT in grass
        // OR if they are in grass, use a much stricter check
        bool canSeePlayer = guard.HasLineOfSightToPlayer();

        if (canSeePlayer)
        {
            float distance = Vector3.Distance(transform.position, guard.PlayerTransform.position);
            float detectionChance = 0f;

            if (playerInGrass)
            {
                // If we can see them through grass (shouldn't happen), use VERY low chance
                Debug.LogWarning($"Can see player through grass? This shouldn't happen! Check grass collider.");
                detectionChance = 0.05f; // Only 5% chance even if visible through grass
            }
            else
            {
                // Normal detection when not in grass
                if (distance < 3f) detectionChance = 0.8f;
                else if (distance < 5f) detectionChance = 0.5f;
                else if (distance < 8f) detectionChance = 0.2f;
                else detectionChance = 0.1f;
            }

            if (Random.value < detectionChance)
            {
                Debug.Log($"BT: Saw player! (Chance: {detectionChance * 100}%)");
                isSuspicious = false;
                currentAction = "Chasing";
                return Node.Status.FAILURE;
            }
            else
            {
                Debug.Log($"BT: Missed player (Chance to spot was {detectionChance * 100}%)");
            }
        }
        else
        {
            if (playerInGrass)
            {
                Debug.Log("Player hidden in grass - safe");
            }
        }

        if (suspiciousTimer <= 0)
        {
            isSuspicious = false;
            currentAction = "Patrolling";
            return Node.Status.SUCCESS;
        }

        return Node.Status.RUNNING;
    }

    #endregion

    #region Seeing Player

    // Condition methods for the tree (return SUCCESS/FAILURE)
    private Node.Status CanSeePlayerCondition()
    {
        if (guard == null || guard.PlayerTransform == null)
            return Node.Status.FAILURE;

        // slow down the autromatic seeing
        if (isSuspicious && suspiciousTimer > 0)
        {
            // Only check for player if we're in the final phase of investigation
            if (suspiciousTimer > 5.0f) // Still early in investigation
            {
                return Node.Status.FAILURE; // Stay suspicious, don't chase
            }
        }

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

        // Check if we've reached a waypoint and should start waiting
        if (agent != null && !agent.pathPending && agent.remainingDistance < 0.5f && !isWaiting)
        {
            // Reached waypoint - start waiting
            if (waypoints != null && waypoints.Length > 0)
            {
                isWaiting = true;
                waitTimer = Random.Range(minWaitTime, maxWaitTime);
                Debug.Log($"Reached waypoint {currentWaypointIndex}. Waiting for {waitTimer:F1} seconds...");
            }
        }

        return Node.Status.RUNNING;
    }

    #endregion

    #region Waypoint Waiting

    private Node.Status IsWaitingCondition()
    {
        if (isWaiting && waitTimer > 0)
        {
            return Node.Status.SUCCESS;
        }
        return Node.Status.FAILURE;
    }

    private Node.Status WaitAction()
    {
        if (!isWaiting) return Node.Status.FAILURE;

        waitTimer -= Time.deltaTime;
        currentAction = "Waiting";

        // Stop the agent while waiting
        if (agent != null && agent.hasPath)
        {
            agent.ResetPath();
        }

        // Optional: Look around while waiting
        transform.Rotate(0, 10 * Time.deltaTime, 0); // Slow look around

        // If wait time is up, move to next waypoint
        if (waitTimer <= 0)
        {
            isWaiting = false;
            currentAction = "Moving to next waypoint";

            // Move to next waypoint
            if (waypoints != null && waypoints.Length > 0)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                Transform nextWaypoint = waypoints[currentWaypointIndex];
                if (nextWaypoint != null)
                {
                    Seek(nextWaypoint.position);
                    Debug.Log($"Moving to waypoint {currentWaypointIndex} after waiting");
                }
            }

            return Node.Status.SUCCESS;
        }

        return Node.Status.RUNNING;
    }

    #endregion

    #region Behavior Tree Code

    private void ConstructBehaviourTree()
    {
        tree = new BehaviourTree("BTBrain Tree");
        Selector root = new Selector("Root Selector");

        // 1. SUSPICIOUS (NEW HIGHEST PRIORITY when investigating noise)
        Sequence suspiciousSequence = new Sequence("Suspicious Sequence");
        suspiciousSequence.AddChild(new Leaf("Is Suspicious?", IsSuspiciousCondition));
        suspiciousSequence.AddChild(new Leaf("Investigate", SuspiciousAction));

        // 2. CHASE BEHAVIOR (second priority)
        Sequence chaseSequence = new Sequence("Chase Sequence");
        chaseSequence.AddChild(new Leaf("Can See Player?", CanSeePlayerCondition));
        chaseSequence.AddChild(new Leaf("Chase Player", ChasePlayerAction));

        // 3. SEARCH BEHAVIOUR
        Sequence searchSequence = new Sequence("Search Sequence");
        searchSequence.AddChild(new Leaf("Lost Player?", LostPlayerCondition));
        searchSequence.AddChild(new Leaf("Search at Last Position", SearchAction));

        // 4. WAIT BEHAVIOR 
        Sequence waitSequence = new Sequence("Wait Sequence");
        waitSequence.AddChild(new Leaf("Is Waiting?", IsWaitingCondition));
        waitSequence.AddChild(new Leaf("Wait at Waypoint", WaitAction));

        // 5. PATROL BEHAVIOR (lowest priority)
        Sequence patrolSequence = new Sequence("Patrol Sequence");
        patrolSequence.AddChild(new Leaf("Patrol", PatrolAction));

        // Add in NEW ORDER
        root.AddChild(suspiciousSequence);
        root.AddChild(chaseSequence);
        root.AddChild(searchSequence);
        root.AddChild(waitSequence);
        root.AddChild(patrolSequence);

        tree.AddChild(root);

        //tree.PrintTree();
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