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

        // Get the NavMeshAgent from the guard
        agent = guard.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            originalSpeed = agent.speed;
        }

        // Get waypoints from the guard's patrol points (you may need to set these)
        // For now, try to find waypoints in children or leave as is

        SetSuspicionTime(Random.Range(suspicionTimeMin, suspicionTimeMax));

        if (waypoints != null && waypoints.Length > 0)
        {
            currentWaypointIndex = 0;
            if (agent != null && waypoints[0] != null)
            {
                agent.SetDestination(waypoints[0].position);
            }
        }

        currentState = GuardState.Patrolling;
        hasInitialised = true;
        Debug.Log("FSM Initialized");
    }


    // NEW: Init with player
    public override void Init(Guard guard, Player _player)
    {
        this.guard = guard;
        player = _player;


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

        // Get raycast debugger
        RaycastBodyDebugger bodyDebugger = GetComponent<RaycastBodyDebugger>();
        if (bodyDebugger == null)
        {
            bodyDebugger = gameObject.AddComponent<RaycastBodyDebugger>();
        }

        // Get positions
        Vector3 eyePosition = guard.RayCastStartLocation != null
            ? guard.RayCastStartLocation.position
            : transform.position + Vector3.up * 1.5f;

        // Define 5 body parts with heights and colors
        BodyPart[] bodyParts = new BodyPart[]
        {
        new BodyPart { name = "Head", height = 1.7f, color = Color.magenta },
        new BodyPart { name = "Chest", height = 1.3f, color = Color.green },
        new BodyPart { name = "Waist", height = 1.0f, color = Color.yellow },
        new BodyPart { name = "Hips", height = 0.7f, color = Color.cyan },
        new BodyPart { name = "Knees", height = 0.4f, color = new Color(1f, 0.5f, 0f) }
        };

        float detectionRange = guard.GetDetectionRange();
        float halfFOV = guard.GetFieldOfView() / 2f;

        bool canSeeAny = false;
        string hitBodyPart = "";

        // Draw detection range (ORANGE)
        Debug.DrawRay(eyePosition, transform.forward * detectionRange, new Color(1f, 0.5f, 0f), 0.1f);

        // Draw FOV boundaries (PURPLE)
        Vector3 leftBoundary = Quaternion.Euler(0, -halfFOV, 0) * transform.forward * detectionRange;
        Vector3 rightBoundary = Quaternion.Euler(0, halfFOV, 0) * transform.forward * detectionRange;
        Debug.DrawRay(eyePosition, leftBoundary, new Color(0.8f, 0.2f, 0.8f), 0.1f);
        Debug.DrawRay(eyePosition, rightBoundary, new Color(0.8f, 0.2f, 0.8f), 0.1f);

        // Raycast to each body part
        foreach (BodyPart part in bodyParts)
        {
            Vector3 targetPosition = player.transform.position + Vector3.up * part.height;
            float distance = Vector3.Distance(eyePosition, targetPosition);
            Vector3 direction = (targetPosition - eyePosition).normalized;
            float angle = Vector3.Angle(transform.forward, direction);

            // Draw line to each body part (colored by body part)
            Debug.DrawRay(eyePosition, direction * distance, part.color, 0.15f);

            // Draw target point marker
            Debug.DrawRay(targetPosition, Vector3.up * 0.2f, part.color, 0.15f);
            Debug.DrawRay(targetPosition, Vector3.down * 0.2f, part.color, 0.15f);

            // Check if in range and FOV
            if (distance <= detectionRange && angle <= halfFOV)
            {
                RaycastHit hit;
                if (Physics.Raycast(eyePosition, direction, out hit, distance))
                {
                    // Draw hit marker
                    Debug.DrawLine(eyePosition, hit.point, part.color, 0.3f);
                    DrawHitMarker(hit.point, part.color);

                    // Record hit
                    bool isPlayer = hit.transform == player.transform;
                    bodyDebugger.RecordHit(hit.point, part.name, hit.transform.name, hit.distance, isPlayer);

                    if (isPlayer)
                    {
                        canSeeAny = true;
                        hitBodyPart = part.name;
                        Debug.Log($" HIT {part.name}! Can see player!");
                        break;
                    }
                    else
                    {
                        Debug.Log($" {part.name} blocked by: {hit.transform.name}");
                    }
                }
                else
                {
                    // Record miss
                    bodyDebugger.RecordMiss(targetPosition, part.name, distance);
                    Debug.DrawRay(eyePosition, direction * distance, Color.gray, 0.15f);
                }
            }
            else
            {
                // Draw reason why not checked
                if (distance > detectionRange)
                {
                    Debug.DrawRay(eyePosition, direction * detectionRange, new Color(0.5f, 0.5f, 0.5f), 0.1f);
                }
            }
        }

        guard.CanSeePlayer = canSeeAny;

        // Debug summary
        if (Time.frameCount % 60 == 0)
        {
            float closestDistance = Vector3.Distance(eyePosition, player.transform.position + Vector3.up * 1.0f);
            Debug.Log($"=== BODY PART DEBUG ===");
            Debug.Log($"Can see player: {canSeeAny}");
            if (canSeeAny)
                Debug.Log($"Hit body part: {hitBodyPart}");
            Debug.Log($"Distance to player: {closestDistance:F1}m");
            Debug.Log($"Detection range: {detectionRange:F1}m");
        }

        if (canSeeAny)
        {
            Debug.Log($" SAW PLAYER! Switching to CHASE");
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

    // Helper method to draw hit marker
    private void DrawHitMarker(Vector3 position, Color color)
    {
        float size = 0.25f;
        Debug.DrawRay(position, Vector3.left * size, color, 0.3f);
        Debug.DrawRay(position, Vector3.right * size, color, 0.3f);
        Debug.DrawRay(position, Vector3.up * size, color, 0.3f);
        Debug.DrawRay(position, Vector3.down * size, color, 0.3f);
        Debug.DrawRay(position, Vector3.forward * size, color, 0.3f);
        Debug.DrawRay(position, Vector3.back * size, color, 0.3f);
    }

    // BodyPart class
    private class BodyPart
    {
        public string name;
        public float height;
        public Color color;
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

    private void DebugRaycastHit(Vector3 origin, Vector3 direction, float distance, RaycastHit hit, string targetName)
    {
        // Draw the raycast line
        Debug.DrawLine(origin, hit.point, Color.red, 0.5f);

        // Draw a sphere at the hit point
        Debug.DrawRay(hit.point, Vector3.up * 0.3f, Color.red, 0.5f);
        Debug.DrawRay(hit.point, Vector3.down * 0.3f, Color.red, 0.5f);
        Debug.DrawRay(hit.point, Vector3.left * 0.3f, Color.red, 0.5f);
        Debug.DrawRay(hit.point, Vector3.right * 0.3f, Color.red, 0.5f);
        Debug.DrawRay(hit.point, Vector3.forward * 0.3f, Color.red, 0.5f);
        Debug.DrawRay(hit.point, Vector3.back * 0.3f, Color.red, 0.5f);

        // Log warning with details
        Debug.LogWarning($"=== RAYCAST HIT DETECTED ===");
        Debug.LogWarning($"Hit object: {hit.transform.name}");
        Debug.LogWarning($"Hit tag: {hit.transform.tag}");
        Debug.LogWarning($"Hit point: {hit.point}");
        Debug.LogWarning($"Distance: {hit.distance:F2} / {distance:F2}");
        Debug.LogWarning($"Normal: {hit.normal}");
        Debug.LogWarning($"Target was: {targetName}");

        // Check if it's the player
        if (hit.transform == player.transform)
        {
            Debug.Log($" HIT THE PLAYER! ");
        }
        else
        {
            Debug.LogWarning($" BLOCKED BY: {hit.transform.name} (Tag: {hit.transform.tag})");

            // Draw extra visual for the blocker
            Renderer blockerRenderer = hit.transform.GetComponent<Renderer>();
            if (blockerRenderer != null)
            {
                // Highlight the blocker temporarily
                StartCoroutine(HighlightObject(hit.transform, Color.red, 0.3f));
            }
        }
    }

    private System.Collections.IEnumerator HighlightObject(Transform obj, Color color, float duration)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null) yield break;

        Material originalMaterial = renderer.material;
        renderer.material.color = color;
        yield return new WaitForSeconds(duration);
        renderer.material = originalMaterial;
    }

    

    private void DrawHitMarker(Vector3 position, string objectName, float distance, bool isPlayer)
    {
        Color color = isPlayer ? Color.green : (objectName == "NO HIT" ? Color.gray : Color.red);

        // Draw cross
        Debug.DrawRay(position, Vector3.left * 0.3f, color, 0.5f);
        Debug.DrawRay(position, Vector3.right * 0.3f, color, 0.5f);
        Debug.DrawRay(position, Vector3.up * 0.3f, color, 0.5f);
        Debug.DrawRay(position, Vector3.down * 0.3f, color, 0.5f);
        Debug.DrawRay(position, Vector3.forward * 0.3f, color, 0.5f);
        Debug.DrawRay(position, Vector3.back * 0.3f, color, 0.5f);

        // Log to console
        if (isPlayer)
        {
            Debug.Log($" HIT PLAYER! Distance: {distance:F2}m at {position}");
        }
        else if (objectName == "NO HIT")
        {
            Debug.Log($" NO HIT - Nothing detected at {distance:F2}m");
        }
        else
        {
            Debug.Log($" HIT: {objectName} at {distance:F2}m - Position: {position}");
        }
    }


}