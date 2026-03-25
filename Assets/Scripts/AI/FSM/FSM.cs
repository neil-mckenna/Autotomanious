using UnityEngine;
using UnityEngine.AI;

// a simple Fixed State Machine brain
public class FSM : AIBrain
{
    #region GuardState Enum

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
    private float searchDuration = 3f;
    private bool hasInitialised = false;
    private bool wasPlayerVisible = false;

    // Waiting
    private bool isWaiting = false;
    private float waitTimer = 0f;
    private float minWaitTime = 2f;
    private float maxWaitTime = 10f;

    [SerializeField][Range(1f,30f)] float suspicionTimeMin = 5f;
    [SerializeField][Range(1f,30f)] float suspicionTimeMax = 5f;

    



    public string GetCurrentState()
    {
        return currentState.ToString();
    }

    protected override void OnPlayerSet()
    {
        Debug.Log($"FSM: Player set - ready to chase!");
    }

    #endregion

    #region Init
    public override void Init(Guard guard)
    {
        this.guard = guard;

        //Debug.Log($"FSM Init called - Guard: {guard?.name}");


        SetSuspicionTime(Random.Range(suspicionTimeMin, suspicionTimeMax));

        if (waypoints != null && waypoints.Length > 0)
        {
            currentWaypointIndex = 0;
            if (agent != null && waypoints[0] != null)
            {
                agent.SetDestination(waypoints[0].position);
                //Debug.Log($"FSM: Moving to first waypoint at {waypoints[0].position}");
            }
        }
        else
        {
            Debug.LogWarning("FSM: No waypoints!");
        }

        currentState = GuardState.Patrolling;
        Invoke(nameof(StartFirstPatrol), 0.2f);
        hasInitialised = true;
        //Debug.Log("FSM Initialized - Starting Patrol");
    }

    // NEW: Init with player
    public override void Init(Guard guard, Player _player)
    {

        // Call base to set guard and player
        base.Init(guard, _player);

        //Debug.Log($"FSM Init with player: {_player?.name}");

        SetSuspicionTime(Random.Range(suspicionTimeMin, suspicionTimeMax));

        if (waypoints != null && waypoints.Length > 0)
        {
            currentWaypointIndex = 0;
            if (agent != null && waypoints[0] != null)
            {
                agent.SetDestination(waypoints[0].position);
                Debug.Log($"FSM: Moving to first waypoint at {waypoints[0].position}");
            }
        }
        else
        {
            Debug.LogWarning("FSM: No waypoints!");
        }

        currentState = GuardState.Patrolling;
        Invoke(nameof(StartFirstPatrol), 0.2f);
        hasInitialised = true;
        Debug.Log("FSM Initialized - Starting Patrol");
    }

    private void StartFirstPatrol()
    {
        if (agent != null && waypoints != null && waypoints.Length > 0)
        {
            agent.SetDestination(waypoints[0].position);
        }
    }
    #endregion

    #region Think
    public override void Think()
    {
        // update this variable

        if (!hasInitialised || guard == null || agent == null)
        {
            Debug.LogError($" Why {hasInitialised} , {guard.name} , {agent} ?");
            return;

        }

        
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"FSM: State={currentState}, Player={(Player != null ? "YES" : "NO")}, SuspiciousTimer={suspiciousTimer}");
        }

        // Check for kill every frame
        CheckForKill();

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
        if (!TryGetPlayer(out Player currentPlayer))
        {
            Wander();
            return;
        }

        float distance = Vector3.Distance(transform.position, Player.transform.position);
        

        float angle = Vector3.Angle(transform.forward, (Player.transform.position - transform.position).normalized);
        float halfFOV = guard.GetFieldOfView() / 2f;

        // CRITICAL DEBUG - Check every frame when close
        if (distance < guard.GetDetectionRange())
        {
            Debug.Log($"=== VISION CHECK ===");
            Debug.Log($"Distance: {distance:F1} / {guard.GetDetectionRange()}");
            Debug.Log($"Angle: {angle:F1}° / Half FOV: {halfFOV}°");
            Debug.Log($"In FOV: {angle <= halfFOV}");
            Debug.Log($"CanSeePlayer: {guard.CanSeePlayer}");
            Debug.Log($"Current State: {currentState}");

            // Check raycast manually
            Vector3 direction = (Player.transform.position - transform.position).normalized;
            RaycastHit hit;
            if (Physics.Raycast(transform.position, direction, out hit, distance))
            {
                Debug.Log($"Raycast hit: {hit.transform.name} (Tag: {hit.transform.tag})");
                Debug.Log($"Is player: {hit.transform == Player.transform}");
            }
        }

        if (guard.CanSeePlayer)
        {
            Debug.Log($"SHOULD CHASE! Distance: {distance:F1} ");
            currentState = GuardState.Chasing;
            lastKnownPlayerPosition = Player.transform.position;
            isWaiting = false;

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
            // Log why can't see
            if (distance <= guard.GetDetectionRange())
            {
                angle = Vector3.Angle(transform.forward, (Player.transform.position - transform.position).normalized);
                if (angle > guard.GetFieldOfView() / 2f)
                {
                    Debug.Log($"  Player in range but OUT of FOV! Angle: {angle:F1}°, Half FOV: {guard.GetFieldOfView() / 2f}°");
                }
                else
                {
                    Debug.Log($"  Player in range and in FOV but line of sight blocked!");
                    // Check raycast
                    Vector3 dir = (Player.transform.position - transform.position).normalized;
                    RaycastHit hit;
                    if (Physics.Raycast(transform.position, dir, out hit, distance))
                    {
                        Debug.Log($"    Raycast hit: {hit.transform.name} (Tag: {hit.transform.tag})");
                    }
                }
            }

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

        if (agent != null && agent.hasPath)
        {
            agent.ResetPath();
        }

        transform.Rotate(0, 10 * Time.deltaTime, 0);

        if (waitTimer <= 0)
        {
            isWaiting = false;
            Debug.Log("FSM: Wait time ended - moving to next waypoint");
        }
    }
    #endregion

    #region Suspicious
    public override void SetSuspicious(Vector3 position, float duration)
    {
        base.SetSuspicious(position, duration);
        currentState = GuardState.Suspicious;
        Debug.Log($"FSM Guard suspicious at {position} from noise");
    }

    protected override void UpdateSuspicious()
    {
        if (Player.transform == null)
        {
            Wander();
            return;
        }

        suspiciousTimer -= Time.deltaTime;
        isWaiting = false;

        // PHASE 1: First half - LOOK AROUND
        if (suspiciousTimer > suspicionDuration / 2f)
        {
            if (agent != null && agent.hasPath)
            {
                agent.ResetPath();
            }
            transform.Rotate(0, 30 * Time.deltaTime, 0);
        }
        // PHASE 2: Second half - INVESTIGATE
        else
        {
            agent.speed = guard.GetDetectionRange() * 0.5f; // Half speed when investigating

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

        // Check if we see the player
        
        bool playerInGrass = IsPlayerInGrass();

        if (guard.CanSeePlayer)
        {
            float distance = Vector3.Distance(transform.position, Player.transform.position);
            float detectionChance = 0f;

            if (playerInGrass)
            {
                if (distance < 3f) detectionChance = 0.1f;
                else if (distance < 5f) detectionChance = 0.05f;
                else detectionChance = 0.01f;
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
                Debug.Log($"FSM: SAW player while suspicious! Switching to CHASE");
                currentState = GuardState.Chasing;
                lastKnownPlayerPosition = Player.transform.position;
                return;
            }
        }

        if (suspiciousTimer <= 0)
        {
            Debug.Log("FSM: Suspicion ended - back to patrol");
            currentState = GuardState.Patrolling;
            agent.speed = guard.GetDetectionRange(); // Reset speed
        }
    }
    #endregion

    #region Chase
    public override void Chase(Transform target)
    {
        if (target != null && agent != null)
        {
            Seek(target.position);
        }
    }

    private void UpdateChasing()
    {
        if (player == null) return;

        if (!TryGetPlayer(out Player currentPlayer))
        {
            currentState = GuardState.Patrolling;
            return;
        }

        // Debug where the guard thinks the player is
        if (Time.frameCount % 30 == 0)
        {
            Debug.Log($"CHASING: Guard at {transform.position}");
            Debug.Log($"CHASING: Player at {player.transform.position}");
            Debug.Log($"CHASING: Destination = {agent.destination}");
            Debug.Log($"CHASING: Has line of sight = {HasLineOfSightToPlayer()}");
        }

        Chase(player.transform);
        isWaiting = false;

        if (HasLineOfSightToPlayer())
        {
            if (GameManager.Instance != null)
                GameManager.Instance.PlayerSpotted();
        }

        if (!HasLineOfSightToPlayer())
        {
            Debug.Log($"LOST PLAYER! Last known position = {lastKnownPlayerPosition}");
            currentState = GuardState.Searching;
            searchTimer = searchDuration;
            lastKnownPlayerPosition = player.transform.position;  // Update with actual player position

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
        if (Player == null) return;

        searchTimer -= Time.deltaTime;
        isWaiting = false;

        if (agent != null && !agent.pathPending && agent.remainingDistance < 0.5f)
        {
            Vector3 searchPosition = lastKnownPlayerPosition + Random.insideUnitSphere * 5f;
            searchPosition.y = lastKnownPlayerPosition.y;
            Seek(searchPosition);
        }

        // If we find the player, chase them
        if (HasLineOfSightToPlayer())
        {
            Debug.Log("FSM: Found player while searching! Switching to CHASE");
            currentState = GuardState.Chasing;
            return;
        }

        if (searchTimer <= 0)
        {
            Debug.Log("FSM: Search ended - back to patrol");
            currentState = GuardState.Patrolling;
        }
    }
    #endregion

    #region Wander
    public override void Wander()
    {
        if (agent == null) return;

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            Transform nextWaypoint = GetNextWaypoint();
            if (nextWaypoint != null)
            {
                Seek(nextWaypoint.position);
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
    #endregion
}