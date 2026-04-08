using UnityEngine;
using UnityEngine.AI;
using System.Collections;

// ============================================================================
// FSM - FINITE STATE MACHINE AI BRAIN (NO ALERT SYSTEM)
// ============================================================================
// 
// This class implements a Finite State Machine-based AI with the following states:
// 
// STATE MACHINE FLOW:
//                    +---------------------------------------------------------+
//                    |                                                         |
//                    v                                                         |
//   +----------+   +------------+   +--------+   +----------+                 |
//   |Patrolling|-->| Suspicious |-->| Chasing|-->| Searching|-+               |
//   +----------+   +------------+   +--------+   +----------+ |               |
//        |              |               |              |       |               |
//        +--------------+---------------+--------------+-------+               |
//                    (Return to patrol when done)                               |
//                                                                               |
// STATE DESCRIPTIONS:                                                           |
// 1. Patrolling - Default state, moves between waypoints or wanders            |
// 2. Suspicious - Heard a noise, investigates area (INDIVIDUAL response only)  |
// 3. Chasing - Has visual on player, actively pursues                          |
// 4. Searching - Lost player, searches last known position                     |
// 5. None - Uninitialized state (should not occur)                             |
//                                                                               |
// KEY DIFFERENCES FROM BTBrain (for scientific comparison):                    |
// - NO alert system (does NOT bark/call for backup)                            |
// - Each FSM guard reacts independently to threats                             |
// - Simpler, more predictable behavior                                         |
// - Lower computational overhead                                               |
// - Individual decision making only                                            |
//                                                                               |
// SCIENTIFIC PURPOSE:                                                          |
// This allows comparison between:                                              |
// - Coordinated AI (BTBrain with alerts) vs Individual AI (FSM without alerts) |
// - Measuring player success rates against different AI coordination levels    |
// - Testing difficulty scaling between isolated and networked enemies          |
//                                                                               |
// ============================================================================

public class FSM : AIBrain
{
    // ========================================================================
    // PRIVATE ENUMS
    // ========================================================================

    private enum GuardState { None, Patrolling, Suspicious, Chasing, Searching }

    // ========================================================================
    // PRIVATE FIELDS - STATE TRACKING
    // ========================================================================

    private GuardState currentState = GuardState.Patrolling;
    private float searchTimer = 5f;
    private bool hasInitialised = false;
    private bool isWaiting = false;
    private float waitTimer = 0f;
    private float minWaitTime = 2f;
    private float maxWaitTime = 5f;
    private bool wasSuspiciousLastFrame = false;
    private bool wasInSuspiciousState = false;

    // ========================================================================
    // SERIALIZED FIELDS - FSM SETTINGS
    // ========================================================================

    [Header("=== FSM SETTINGS ===")]
    [SerializeField][Range(1f, 30f)] private float suspicionTimeMin = 5f;
    [SerializeField][Range(1f, 30f)] private float suspicionTimeMax = 5f;
    [SerializeField] private float chaseBufferTime = 3f;
    [SerializeField] private bool showFSMDebug = true;

    [Header("=== INVESTIGATION SETTINGS ===")]
    [SerializeField] private float investigationRotateSpeed = 60f;
    [SerializeField] private float investigationDuration = 3f;

    // ========================================================================
    // PUBLIC PROPERTIES
    // ========================================================================

    public string GetCurrentState() => currentState.ToString();

    // ========================================================================
    // INITIALIZATION METHODS
    // ========================================================================

    public override void Init(Guard guard)
    {
        this.guard = guard;
        agent = guard.GetComponent<NavMeshAgent>();

        if (agent != null) originalSpeed = agent.speed;

        SetSuspicionTime(Random.Range(suspicionTimeMin, suspicionTimeMax));

        if (waypoints != null && waypoints.Length > 0)
        {
            currentWaypointIndex = 0;
            agent?.SetDestination(waypoints[0].position);
        }

        currentState = GuardState.Patrolling;
        hasInitialised = true;
        if (showFSMDebug)
        {
            //Debug.Log("[FSM] Initialized - Individual AI (no alert system)");
        }
    }

    public override void Init(Guard guard, Player _player)
    {
        Init(guard);
        player = _player;
        if (showFSMDebug)
        {
            //Debug.Log($"[FSM] Player set: {_player?.name} - Individual responses only");
        }
    }

    // ========================================================================
    // CORE UPDATE LOOP
    // ========================================================================

    public override void Think()
    {
        if (!hasInitialised || guard == null || agent == null) return;
        if (player == null) TryGetPlayer(out player);

        if (isSuspicious && showFSMDebug && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[FSM THINK] isSuspicious = TRUE, State = {currentState}, Time = {Time.time}");
        }

        // ========== VISION DETECTION (HIGHEST PRIORITY) ==========
        bool canSeePlayer = HasLineOfSightToPlayer();

        if (guard != null)
            guard.CanSeePlayer = canSeePlayer;

        // VISUAL DETECTION = IMMEDIATE CHASE (NO ALERT - Individual response only)
        if (canSeePlayer)
        {
            if (currentState != GuardState.Chasing)
            {
                Debug.Log("[FSM] VISUAL DETECTION - Player spotted! CHASING (Individual response)");
                currentState = GuardState.Chasing;
                lastKnownPlayerPosition = player.transform.position;
                lastSeenTime = Time.time;
                isWaiting = false;

                if (GameManager.Instance != null)
                    GameManager.Instance.GuardAlerted();
            }
        }

        // ========== HEARING DETECTION ==========
        if (isSuspicious && Time.frameCount % 60 == 0 && showFSMDebug)
        {
            Debug.Log($"[HEARING STATUS] Guard: {gameObject.name}, State: {currentState}, isSuspicious: {isSuspicious}, Timer: {suspiciousTimer:F1}s");
            Debug.Log($"  Suspicious Position: {suspiciousPosition}");
            Debug.Log($"  Distance to suspicious point: {Vector3.Distance(transform.position, suspiciousPosition):F2}m");
        }

        // Transition to Suspicious state from ANY state except Chasing
        if (!canSeePlayer && isSuspicious && currentState != GuardState.Chasing)
        {
            if (currentState != GuardState.Suspicious && currentState != GuardState.Searching)
            {
                float distanceToNoise = Vector3.Distance(transform.position, suspiciousPosition);

                Debug.Log($"<color=yellow>[FSM] HEARING DETECTION!</color>");
                Debug.Log($"  Guard: {gameObject.name}");
                Debug.Log($"  Current State: {currentState} -> SUSPICIOUS");
                Debug.Log($"  Noise Position: {suspiciousPosition}");
                Debug.Log($"  Distance to noise: {distanceToNoise:F2}m");
                Debug.Log($"  Suspicion Duration: {suspiciousTimer:F1}s");

                currentState = GuardState.Suspicious;

                if (agent != null)
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }

                isWaiting = false;
                lastKnownPlayerPosition = suspiciousPosition;
            }
            else if (currentState == GuardState.Suspicious)
            {
                float distToCurrentTarget = agent != null && agent.hasPath ? agent.remainingDistance : 0f;
                float distToNoise = Vector3.Distance(transform.position, suspiciousPosition);

                if (distToNoise > 15f && distToCurrentTarget < 1f)
                {
                    Debug.Log($"[HEARING] Updating investigation point to {suspiciousPosition}");
                    agent.SetDestination(suspiciousPosition);
                }
            }
        }

        // Emergency fix
        if (isSuspicious && currentState != GuardState.Suspicious && currentState != GuardState.Chasing && currentState != GuardState.Searching)
        {
            Debug.LogWarning($"[EMERGENCY FIX] isSuspicious = true but state = {currentState}! Forcing SUSPICIOUS");
            currentState = GuardState.Suspicious;
            if (agent != null)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
        }

        // Handle GameManager events
        if (canSeePlayer && !wasPlayerVisible)
        {
            wasPlayerVisible = true;
            lastSeenTime = Time.time;
            if (GameManager.Instance != null)
                GameManager.Instance.PlayerSpotted();
        }
        else if (!canSeePlayer && wasPlayerVisible)
        {
            wasPlayerVisible = false;
            if (GameManager.Instance != null)
                GameManager.Instance.GuardLostPlayer();
        }

        // Check for kill
        if (canSeePlayer)
        {
            CheckForKill();
        }

        // ========== STATE EXECUTION ==========
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
            default:
                if (showFSMDebug) Debug.LogWarning($"[FSM] Unknown state: {currentState}");
                break;
        }
    }

    // ========================================================================
    // PATROLLING STATE
    // ========================================================================

    private void UpdatePatrolling()
    {
        if (player == null) { Wander(); return; }

        if (isWaiting) UpdateWaiting();
        else Wander();

        if (!isWaiting && agent != null && !agent.pathPending && agent.remainingDistance < 0.5f)
        {
            if (waypoints != null && waypoints.Length > 0)
            {
                isWaiting = true;
                waitTimer = Random.Range(minWaitTime, maxWaitTime);
                if (showFSMDebug) Debug.Log("[FSM] Reached waypoint. Waiting...");
            }
        }
    }

    private void UpdateWaiting()
    {
        if (!isWaiting) return;
        waitTimer -= Time.deltaTime;
        agent?.ResetPath();
        transform.Rotate(0, 10 * Time.deltaTime, 0);

        if (waitTimer <= 0)
        {
            isWaiting = false;
            Transform nextWaypoint = GetNextWaypoint();
            if (nextWaypoint != null) Seek(nextWaypoint.position);
            if (showFSMDebug) Debug.Log("[FSM] Wait ended, moving to next waypoint");
        }
    }

    // ========================================================================
    // SUSPICIOUS STATE
    // ========================================================================

    private void UpdateSuspicious()
    {
        if (player == null) { Wander(); return; }

        if (!wasInSuspiciousState && currentState == GuardState.Suspicious)
        {
            Debug.Log($"<color=cyan>[SUSPICIOUS START] Guard: {gameObject.name}</color>");
            Debug.Log($"  Investigating: {suspiciousPosition} (Individual investigation)");
            Debug.Log($"  Distance: {Vector3.Distance(transform.position, suspiciousPosition):F2}m");
            Debug.Log($"  Duration: {suspiciousTimer:F1}s");
            wasInSuspiciousState = true;
        }

        if (HasLineOfSightToPlayer())
        {
            if (showFSMDebug) Debug.Log("[FSM] Saw player while suspicious! Switching to CHASE");
            currentState = GuardState.Chasing;
            lastKnownPlayerPosition = player.transform.position;
            lastSeenTime = Time.time;
            wasInSuspiciousState = false;
            return;
        }

        suspiciousTimer -= Time.deltaTime;

        if (agent != null && suspiciousPosition != Vector3.zero)
        {
            agent.isStopped = false;

            if (!agent.hasPath || agent.remainingDistance < 0.5f)
            {
                Vector3 targetPos = new Vector3(suspiciousPosition.x, 0.05f, suspiciousPosition.z);

                if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                    if (showFSMDebug) Debug.Log($"[SUSPICIOUS] Moving to investigate: {hit.position}");
                }
                else
                {
                    Debug.LogWarning($"[SUSPICIOUS] Cannot path to {suspiciousPosition} - not on NavMesh");
                }
            }

            if (agent.hasPath && agent.remainingDistance > 1f)
            {
                Vector3 directionToTarget = (agent.destination - transform.position).normalized;
                directionToTarget.y = 0;
                if (directionToTarget != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(directionToTarget);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, investigationRotateSpeed * Time.deltaTime);
                }
            }
            else if (agent.hasPath && agent.remainingDistance <= 1f)
            {
                transform.Rotate(0, investigationRotateSpeed * Time.deltaTime, 0);
            }
        }

        if (suspiciousTimer <= 0)
        {
            if (showFSMDebug) Debug.Log("[FSM] Suspicion ended - back to patrol");
            currentState = GuardState.Patrolling;
            if (agent != null)
            {
                agent.speed = originalSpeed;
                agent.isStopped = false;
                agent.ResetPath();
            }
            isSuspicious = false;
            wasInSuspiciousState = false;

            Transform nextWaypoint = GetNextWaypoint();
            if (nextWaypoint != null) Seek(nextWaypoint.position);
        }
    }

    // ========================================================================
    // CHASING STATE
    // ========================================================================

    private void UpdateChasing()
    {
        if (player == null) return;

        CheckForKill();
        Chase(player.transform);

        if (HasLineOfSightToPlayer())
        {
            lastKnownPlayerPosition = player.transform.position;
            lastSeenTime = Time.time;
        }

        if (Time.time - lastSeenTime > chaseBufferTime)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
            if (distanceToPlayer > guard.GetDetectionRange())
            {
                if (showFSMDebug) Debug.Log("[FSM] Lost player! Switching to SEARCH");
                currentState = GuardState.Searching;
                searchTimer = 5f;
            }
        }
    }

    // ========================================================================
    // SEARCHING STATE
    // ========================================================================

    private void UpdateSearching()
    {
        if (player == null) return;

        CheckForKill();

        if (HasLineOfSightToPlayer())
        {
            if (showFSMDebug) Debug.Log("[FSM] Found player during search! Switching to CHASE");
            currentState = GuardState.Chasing;
            lastKnownPlayerPosition = player.transform.position;
            lastSeenTime = Time.time;
            return;
        }

        searchTimer -= Time.deltaTime;

        if (agent != null && !agent.pathPending && agent.remainingDistance < 0.5f)
        {
            if (lastKnownPlayerPosition != Vector3.zero)
            {
                Vector3 searchPosition = lastKnownPlayerPosition;
                float searchRadius = 8f;
                Vector3 randomOffset = Random.insideUnitSphere * searchRadius;
                randomOffset.y = 0;
                searchPosition += randomOffset;

                if (NavMesh.SamplePosition(searchPosition, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
                {
                    Seek(hit.position);
                    if (showFSMDebug) Debug.Log($"[SEARCH] Searching area: {hit.position}");
                }
            }
            else
            {
                Wander();
            }
        }

        if (searchTimer <= 0)
        {
            if (showFSMDebug) Debug.Log("[FSM] Search ended - back to patrol");
            currentState = GuardState.Patrolling;
            lastKnownPlayerPosition = Vector3.zero;
        }
    }

    // ========================================================================
    // MOVEMENT BEHAVIORS
    // ========================================================================

    public override void Wander()
    {
        if (agent == null) return;

        agent.speed = originalSpeed;

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            Transform nextWaypoint = GetNextWaypoint();
            if (nextWaypoint != null)
            {
                Seek(nextWaypoint.position);
            }
            else
            {
                Vector3 randomDir = Random.insideUnitSphere * 10f;
                randomDir += transform.position;
                if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, 10, NavMesh.AllAreas))
                    Seek(hit.position);
            }
        }
    }

    public override void Chase(Transform target)
    {
        if (target != null && agent != null)
        {
            if (Mathf.Abs(agent.speed - originalSpeed * 1.5f) > 0.01f)
                agent.speed = originalSpeed * 1.5f;
            Seek(target.position);
        }
    }

    public override void SetSuspicious(Vector3 position, float duration)
    {
        base.SetSuspicious(position, duration);

        if (currentState == GuardState.Patrolling)
        {
            currentState = GuardState.Suspicious;
            if (showFSMDebug) Debug.Log($"[FSM] Heard noise at {position} - Switching to SUSPICIOUS (Individual response)");
        }
    }

    // ========================================================================
    // DEBUG VISUALIZATION
    // ========================================================================

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        if (guard != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, KillRange);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, guard.GetDetectionRange());

            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, guard.GetMaxHearingDistance());
        }

        if (player != null && player.gameObject.activeSelf)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
            float hearingRange = guard != null ? guard.GetMaxHearingDistance() : 0f;
            float detectionRange = guard != null ? guard.GetDetectionRange() : 0f;

            Color lineColor;
            if (distanceToPlayer <= detectionRange)
                lineColor = Color.red;
            else if (distanceToPlayer <= hearingRange)
                lineColor = Color.yellow;
            else
                lineColor = Color.green;

            Gizmos.color = lineColor;
            Gizmos.DrawLine(transform.position, player.transform.position);

            Gizmos.color = new Color(lineColor.r, lineColor.g, lineColor.b, 0.5f);
            Gizmos.DrawWireSphere(player.transform.position, 0.3f);

#if UNITY_EDITOR
            Vector3 midPoint = (transform.position + player.transform.position) / 2;
            midPoint.y += 1f;
            string distanceText = $"{distanceToPlayer:F1}m";

            if (distanceToPlayer <= detectionRange)
                distanceText += "\n<color=red>VISUAL RANGE</color>";
            else if (distanceToPlayer <= hearingRange)
                distanceText += "\n<color=yellow>HEARING RANGE</color>";

            UnityEditor.Handles.Label(midPoint, distanceText);
#endif
        }

        if (waypoints != null)
        {
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] != null)
                {
                    Gizmos.color = i == currentWaypointIndex ? Color.green : Color.blue;
                    Gizmos.DrawWireSphere(waypoints[i].position, 0.3f);
                }
            }
        }
    }
}