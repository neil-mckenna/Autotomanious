using UnityEngine;
using UnityEngine.AI;

// a simple Fixed State Machine brain
public class FSM : AIBrain
{
    #region GuardState Enum

    // enum for state, add TODO ?
    private enum GuardState
    {
        None,
        Patrolling,
        Suspicious,
        Chasing,
        Searching
    }

    #endregion

    #region Properties

    private GuardState currentState = GuardState.Patrolling;
    private float searchTimer;

    [SerializeField] private float wanderSpeed = 3.5f;
    private float searchDuration = 3f;

    
    private bool hasInitialised = false;

    private bool wasPlayerVisible = false;

    private Vector3 suspiciousPosition;
    private float suspiciousTimer;
    private float suspiciousDuration = 3f;

    // waiitng
    private bool isWaiting = false;
    private float waitTimer = 0f;
    private float minWaitTime = 2f;
    private float maxWaitTime = 10f;

    public string GetCurrentState()
    {
        return currentState.ToString();
    }

    #endregion

    #region Init
    public override void Init(Guard guard)
    {
        this.guard = guard;

        if (waypoints != null && waypoints.Length > 0)
        {
            //Debug.Log($"FSM received {waypoints.Length} waypoints");
            // Start at first waypoint
            currentWaypointIndex = 0;
            if (agent != null && waypoints.Length > 0)
            {

                agent.SetDestination(waypoints[0].position);
            }
        }
        else
        {
            //Debug.LogWarning("FSM: No waypoints found!");
        }
        

        currentState = GuardState.Patrolling;
        

        Invoke(nameof(StartFirstPatrol), 0.2f);

        hasInitialised = true;
        //Debug.Log("FSM Initialized - Starting Patrol");
    }

    private void StartFirstPatrol()
    {
        if (agent != null && waypoints != null && waypoints.Length > 0)
        {
            agent.SetDestination(waypoints[0].position);
            //Debug.Log($"Starting patrol to first waypoint: {waypoints[0].name}");
        }

    }

    #endregion

    #region Think

    public override void Think()
    {
        if (!hasInitialised || guard == null || agent == null)
        {
            //Debug.Log($"Think() skipped - hasInitialised: {hasInitialised}, guard: {guard != null}, agent: {agent != null}");
            return;
        }

        // Force debug every frame
        bool canSeePlayer = false;
        if (guard != null)
        {
            canSeePlayer = guard.HasLineOfSightToPlayer();
            //Debug.Log($"<<< FRAME {Time.frameCount} >>> State: {currentState}, Can see player: {canSeePlayer}");
        }

        //Debug.Log($"Think() - Current state: {currentState}");

        switch (currentState)
        {
            case GuardState.Patrolling:
                UpdatePatrolling();
                break;
            case GuardState.Suspicious:
                UpdateSuspicious();
                break;
            case GuardState.Chasing:
                UpdateChasing();
                break;
            case GuardState.Searching:
                UpdateSearching();
                break;
        }
    }

    #endregion

    #region Patrol

    private void UpdatePatrolling()
    {
        bool canSeePlayer = guard.HasLineOfSightToPlayer();

        if (canSeePlayer)
        {
            currentState = GuardState.Chasing;
            lastKnownPlayerPosition = guard.PlayerTransform.position;
            isWaiting = false; // Reset waiting when player spotted

            if (!wasPlayerVisible)
            {
                wasPlayerVisible = true;
                if (GameManager.Instance != null)
                    GameManager.Instance.GuardAlerted();
            }

            if (GameManager.Instance != null)
                GameManager.Instance.PlayerSpotted();

            return;
        }
        else
        {
            if (wasPlayerVisible)
            {
                wasPlayerVisible = false;
                if (GameManager.Instance != null)
                    GameManager.Instance.GuardLostPlayer();
            }
        }

        // Handle waiting at waypoints
        if (isWaiting)
        {
            UpdateWaiting();
        }
        else
        {
            Wander();

            // Check if we've reached a waypoint and should start waiting
            if (agent != null && !agent.pathPending && agent.remainingDistance < 0.5f)
            {
                if (waypoints != null && waypoints.Length > 0)
                {
                    isWaiting = true;
                    waitTimer = Random.Range(minWaitTime, maxWaitTime);
                    Debug.Log($"FSM: Reached waypoint. Waiting for {waitTimer:F1} seconds...");
                }
            }
        }
    }

    #endregion

    #region Waiting

    private void UpdateWaiting()
    {
        if (!isWaiting) return;

        waitTimer -= Time.deltaTime;

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
            Debug.Log("FSM: Wait time ended - moving to next waypoint");

            // Move to next waypoint is handled automatically by Wander() next frame
        }
    }

    #endregion

    #region Suspicious
    public void SetSuspicious(Vector3 position, float duration)
    {
        Debug.Log($"SetSuspicious called! Current state: {currentState}");
        suspiciousPosition = position;
        suspiciousTimer = duration;
        suspiciousDuration = duration;
        currentState = GuardState.Suspicious;
        Debug.Log($"FSM Guard suspicious at {position} from noise");
    }

    private void UpdateSuspicious()
    {
        suspiciousTimer -= Time.deltaTime;
        isWaiting = false;

        // PHASE 1: First half - LOOK AROUND (don't move)
        if (suspiciousTimer > suspiciousDuration / 2f)
        {
            // Stand still and look around
            if (agent != null && agent.hasPath)
            {
                agent.ResetPath();
            }

            // Look around slowly
            transform.Rotate(0, 30 * Time.deltaTime, 0);
        }
        // PHASE 2: Second half - INVESTIGATE with randomness
        else
        {
            // Move at slower speed while investigating
            agent.speed = wanderSpeed * 0.7f;

            // Move to random area around noise
            if (!agent.hasPath || agent.remainingDistance < 1.0f)
            {
                float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float randomDistance = Random.Range(3f, 8f);

                Vector3 randomOffset = new Vector3(
                    Mathf.Cos(randomAngle) * randomDistance,
                    0,
                    Mathf.Sin(randomAngle) * randomDistance
                );

                Vector3 investigatePosition = suspiciousPosition + randomOffset;
                Seek(investigatePosition);
            }
        }

        // Check if player is in grass FIRST
        bool playerInGrass = IsPlayerInGrass();

        // Only check line of sight
        bool canSeePlayer = guard.HasLineOfSightToPlayer();

        if (canSeePlayer)
        {
            float distance = Vector3.Distance(transform.position, guard.PlayerTransform.position);
            float detectionChance = 0f;

            if (playerInGrass)
            {
                // SUPER LOW detection chance when in grass!
                if (distance < 3f) detectionChance = 0.1f;      // Only 10% at close range
                else if (distance < 5f) detectionChance = 0.05f; // 5% at medium range
                else detectionChance = 0.01f;                   // 1% at far range

                Debug.Log($"Player in grass - detection chance: {detectionChance * 100}%");
            }
            else
            {
                // Normal detection when not in grass
                if (distance < 3f) detectionChance = 0.8f;
                else if (distance < 5f) detectionChance = 0.5f;
                else if (distance < 8f) detectionChance = 0.2f;
                else detectionChance = 0.1f;
            }

            // Roll the dice!
            if (Random.value < detectionChance)
            {
                Debug.Log($"FSM: SAW you! ({detectionChance * 100}% chance)");
                currentState = GuardState.Chasing;
                lastKnownPlayerPosition = guard.PlayerTransform.position;
                return;
            }
            else
            {
                Debug.Log($"FSM: MISSED you! (Chance to spot was {detectionChance * 100}%)");
            }
        }
        else
        {
            if (playerInGrass)
            {
                Debug.Log("FSM: Player hidden in grass - safe");
            }
        }

        // Suspicion ends
        if (suspiciousTimer <= 0)
        {
            Debug.Log("Suspicion ended - back to patrol");
            currentState = GuardState.Patrolling;
            agent.speed = wanderSpeed; // Reset speed
        }
    }

    #endregion

    #region Chase

    // actual chase the player
    public override void Chase(Transform target)
    {
        if (target != null && agent != null)
        {
            Seek(target.position);
            //Debug.Log($"Chasing player at {target.position}");
        }
    }

    // chase state handling
    private void UpdateChasing()
    {
        if (guard.PlayerTransform == null) { return; }

        Chase(guard.PlayerTransform);

        // Reset waiting when chasing
        isWaiting = false;

        // keep counting spotted frames while chasing
        if (guard.HasLineOfSightToPlayer())
        {
            if (GameManager.Instance != null)
                GameManager.Instance.PlayerSpotted();
        }

        if (!guard.HasLineOfSightToPlayer())
        {
            currentState = GuardState.Searching;
            searchTimer = searchDuration;
            lastKnownPlayerPosition = guard.PlayerTransform.position;
            Debug.Log("Lost player - searching...");

            if (wasPlayerVisible)
            {
                wasPlayerVisible = false;
                if (GameManager.Instance != null)
                    GameManager.Instance.GuardLostPlayer();
            }
        }
    }

    #endregion

    #region Searching

    private void UpdateSearching()
    {
        searchTimer -= Time.deltaTime;

        // Reset waiting when searching
        isWaiting = false;

        if (agent != null && !agent.pathPending && agent.remainingDistance < 0.5f)
        {
            Vector3 searchPosition = lastKnownPlayerPosition + Random.insideUnitSphere * 5f;
            searchPosition.y = lastKnownPlayerPosition.y;
            Seek(searchPosition);
        }

        if (guard.HasLineOfSightToPlayer())
        {
            currentState = GuardState.Chasing;
            return;
        }

        if (searchTimer <= 0)
        {
            currentState = GuardState.Patrolling;
        }
    }

    #endregion

    #region Wander

    // this wander between random waypoint to simulate variantion and randomness
    public override void Wander()
    {
        if (agent == null) { return; }

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            Transform nextWaypoint = GetNextWaypoint();
            if (nextWaypoint != null)
            {
                Seek(nextWaypoint.position);
                //Debug.Log($"Wandering to waypoint: {nextWaypoint.name}");
            }
            else
            {
                Vector3 randomDirection = Random.insideUnitSphere * 10f;
                randomDirection += transform.position;

                if (NavMesh.SamplePosition(
                    randomDirection,
                    out NavMeshHit hit,
                    10,
                    NavMesh.AllAreas))
                {
                    Seek(hit.position);
                    //Debug.Log("Wandering randomly");

                }
            }
        }
    }

    #endregion

    

}

