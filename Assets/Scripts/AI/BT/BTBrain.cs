using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// Behavior tree AI brain class
public class BTBrain : AIBrain
{
    #region Properties

    [Header("Behaviour Tree Settings")]
    [SerializeField] private float chaseSpeed = 5f;
    [SerializeField] private float wanderSpeed = 3.5f;
    [SerializeField] private float searchDuration = 3f;

    [SerializeField][Range(1f, 30f)] float suspicionTimeMin = 5f;
    [SerializeField][Range(1f, 30f)] float suspicionTimeMax = 5f;

    private BehaviourTree tree;
    private bool hasInitialised = false;

    // State tracking
    private bool wasPlayerVisible = false;
    private float searchTimer;
    private string currentAction = "Initializing...";

    // wait times
    private bool isWaiting = false;
    private float waitTimer = 0f;
    private float minWaitTime = 2f;
    private float maxWaitTime = 10f;

    public string GetCurrentAction()
    {
        return currentAction;
    }

    protected override void OnPlayerSet()
    {
        Debug.Log($"BTBrain: Player set - behavior tree will activate");
    }

    #endregion

    #region Init
    public override void Init(Guard guard)
    {
        this.guard = guard;

        //  Set suspicion time for BT
        SetSuspicionTime(Random.Range(suspicionTimeMin, suspicionTimeMax));

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
    }

    // Init with player
    public override void Init(Guard guard, Player _player)
    {
        base.Init(guard, _player);

        // Set suspicion time for BT
        SetSuspicionTime(Random.Range(suspicionTimeMin, suspicionTimeMax));

        Debug.Log($"BTBrain Init with player: {_player?.name}");

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
    }
    #endregion

    #region Think
    public override void Think()
    {
        if (!hasInitialised || tree == null || guard == null) return;

        // Check if stunned
        if (isStunned) return;

        // Check for kill
        CheckForKill();

        // Process the tree every frame
        Node.Status status = tree.Process();
    }
    #endregion

    #region Hearing Player 
    public override void SetSuspicious(Vector3 position, float duration)
    {
        base.SetSuspicious(position, duration);
        currentAction = "Suspicious";
        Debug.Log($"BT Guard suspicious at {position} from noise");
    }

    // Condition for the tree
    private Node.Status IsSuspiciousCondition()
    {
        if (isSuspicious && suspiciousTimer > 0)
        {
            return Node.Status.SUCCESS;
        }
        return Node.Status.FAILURE;
    }

    // Action for the tree
    private Node.Status SuspiciousAction()
    {
        if (!isSuspicious) return Node.Status.FAILURE;

        suspiciousTimer -= Time.deltaTime;
        currentAction = "Suspicious";

        // Investigate random area
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

        // Use base class vision detection
        
        

        if (guard.CanSeePlayer)
        {
            float distance = Vector3.Distance(transform.position, PlayerTransform.position);
            float detectionChance = 0f;

            if (IsPlayerInGrass())
            {
                detectionChance = 0.05f;
            }
            else
            {
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
    private Node.Status CanSeePlayerCondition()
    {
        if (!TryGetPlayer(out Player currentPlayer))
            return Node.Status.FAILURE;

        if (guard == null) return Node.Status.FAILURE;

        // Don't chase if suspicious and still investigating
        if (isSuspicious && suspiciousTimer > 0)
        {
            if (suspiciousTimer > 2.0f)
            {
                return Node.Status.FAILURE;
            }
        }

        // Use base class vision detection

        if (guard.CanSeePlayer)
        {
            lastKnownPlayerPosition = Player.transform.position;
            currentAction = "Chasing";

            if (!wasPlayerVisible)
            {
                wasPlayerVisible = true;
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.GuardAlerted();
                }
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlayerSpotted();
            }

            return Node.Status.SUCCESS;
        }
        else
        {
            // in grass or not
            if (wasPlayerVisible)
            {
                wasPlayerVisible = false;
                if (GameManager.Instance != null)
                    GameManager.Instance.GuardLostPlayer();
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
    public override void Chase(Transform target)
    {
        if (target != null && agent != null)
        {
            agent.speed = chaseSpeed;
            Seek(target.position);
        }
    }

    private Node.Status ChasePlayerAction()
    {
        if (PlayerTransform == null) return Node.Status.FAILURE;

        Chase(PlayerTransform);
        lastKnownPlayerPosition = PlayerTransform.position;

        //  Use base class vision detection
        if (!guard.CanSeePlayer)
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

        //  Use base class vision detection
        if (guard.CanSeePlayer && guard != null)
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
        Wander();

        //  Use base class vision detection
        if (guard.CanSeePlayer)
        {
            currentAction = "Chasing";
            return Node.Status.FAILURE;
        }

        // Check if we've reached a waypoint and should start waiting
        if (agent != null && !agent.pathPending && agent.remainingDistance < 0.5f && !isWaiting)
        {
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

        if (agent != null && agent.hasPath)
        {
            agent.ResetPath();
        }

        transform.Rotate(0, 10 * Time.deltaTime, 0);

        if (waitTimer <= 0)
        {
            isWaiting = false;
            currentAction = "Moving to next waypoint";

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

        // 1. SUSPICIOUS (highest priority)
        Sequence suspiciousSequence = new Sequence("Suspicious Sequence");
        suspiciousSequence.AddChild(new Leaf("Is Suspicious?", IsSuspiciousCondition));
        suspiciousSequence.AddChild(new Leaf("Investigate", SuspiciousAction));

        // 2. CHASE BEHAVIOR
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

        root.AddChild(suspiciousSequence);
        root.AddChild(chaseSequence);
        root.AddChild(searchSequence);
        root.AddChild(waitSequence);
        root.AddChild(patrolSequence);

        tree.AddChild(root);
    }
    #endregion

    #region Debug
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !hasInitialised || agent == null)
            return;

        Vector3 textPos = transform.position + Vector3.up * 2.5f;

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(textPos, $"BT: {currentAction}");

        if (agent.hasPath && agent.destination != Vector3.zero)
        {
            UnityEditor.Handles.Label(textPos + Vector3.down * 0.5f, $"Dest: {agent.destination}");
        }
#endif

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
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawSphere(waypoints[i].position, 0.3f);

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