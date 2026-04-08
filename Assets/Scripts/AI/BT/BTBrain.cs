using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// ============================================================================
// BTBRAIN - BEHAVIOR TREE AI BRAIN WITH ALERT SYSTEM
// ============================================================================
// 
// This class implements a Behavior Tree-based AI with the following features:
// 1. Behavior Tree decision making (modular, reusable behavior nodes)
// 2. Vision detection (multi-point raycast from base class)
// 3. Hearing detection (noise-based suspicion)
// 4. Patrol with waypoint waiting and look-around animations
// 5. Chase and search behaviors when player is lost
// 6. Anti-collision system (prevents guards from stacking)
// 7. ALERT SYSTEM (BARK) - Notifies nearby guards when player is spotted
// 8. Status effects (stun, blind, slow from base class)
//
// ALERT SYSTEM FEATURES:
// - When a guard spots the player, it alerts all guards within radius
// - Alerted guards become suspicious and investigate the last known position
// - Distance-based reaction delay (closer guards react faster)
// - Cooldown system prevents alert spam
// - Works with all AI types (BTBrain, FSM, Zombie)
//
// BEHAVIOR TREE STRUCTURE (Priority order from highest to lowest):
// 1. Suspicious (Hearing/Alerts) - Investigate noises or alerted positions
// 2. Chase (Vision) - Chase visible player
// 3. Search - Search last known position after losing player
// 4. Wait - Pause at waypoints for realism
// 5. Patrol - Default wander behavior
//
// ============================================================================

public class BTBrain : AIBrain
{
    // ========================================================================
    // SERIALIZED FIELDS - BEHAVIOR TREE SETTINGS
    // ========================================================================

    [Header("=== BEHAVIOR TREE SETTINGS ===")]
    [Tooltip("Movement speed when chasing the player (meters/second)")]
    [SerializeField] private float chaseSpeed = 5f;

    [Tooltip("Movement speed when patrolling (meters/second)")]
    [SerializeField] private float wanderSpeed = 3.5f;

    [Tooltip("How long to search after losing the player (seconds)")]
    [SerializeField] private float searchDuration = 3f;

    [Tooltip("Minimum suspicion duration when hearing a noise (seconds)")]
    [SerializeField][Range(1f, 30f)] private float suspicionTimeMin = 5f;

    [Tooltip("Maximum suspicion duration when hearing a noise (seconds)")]
    [SerializeField][Range(1f, 30f)] private float suspicionTimeMax = 5f;

    // ========================================================================
    // SERIALIZED FIELDS - ANTI-COLLISION SETTINGS
    // ========================================================================

    [Header("=== ANTI-COLLISION SETTINGS ===")]
    [Tooltip("Radius to check for other guards (meters)")]
    [SerializeField] private float antiCollisionRadius = 1.5f;

    [Tooltip("How strongly to push away from other guards")]
    [SerializeField] private float antiCollisionStrength = 3f;

    [Tooltip("Layer mask for guard collision detection")]
    [SerializeField] private LayerMask guardLayerMask;

    [Tooltip("Time before considering guard stuck (seconds)")]
    [SerializeField] private float stuckCheckTime = 2f;

    [Tooltip("Distance threshold for stuck detection (meters)")]
    [SerializeField] private float stuckDistanceThreshold = 0.5f;

    // ========================================================================
    // SERIALIZED FIELDS - ALERT SYSTEM (BARK)
    // ========================================================================

    [Header("=== ALERT SYSTEM (BARK) SETTINGS ===")]
    [Tooltip("Radius within which guards will be alerted when this guard spots the player (meters)")]
    [SerializeField] private float alertRadius = 20f;

    [Tooltip("Minimum time between alerts (prevents spam)")]
    [SerializeField] private float alertCooldown = 3f;

    [Tooltip("Layer mask for other guards to alert")]
    [SerializeField] private LayerMask guardLayerForAlert;

    [Tooltip("Enable debug logging for alert system")]
    [SerializeField] private bool debugAlertSystem = true;

    [Tooltip("Enable alert radius visualization in Scene view")]
    [SerializeField] private bool showAlertRadius = true;

    // ========================================================================
    // PRIVATE FIELDS - ANTI-COLLISION TRACKING
    // ========================================================================

    private Vector3 lastPosition;           // Position from previous frame for stuck detection
    private float stuckTimer = 0f;          // Time spent not moving
    private bool isStuck = false;           // Is guard currently stuck?
    private float antiCollisionTimer = 0f;   // Timer for anti-collision force duration
    private Vector3 antiCollisionForce;      // Calculated avoidance force vector

    // ========================================================================
    // PRIVATE FIELDS - BEHAVIOR TREE
    // ========================================================================

    private BehaviourTree tree;              // Reference to the behavior tree
    private bool hasInitialised = false;     // Has the brain been initialized?

    // ========================================================================
    // PRIVATE FIELDS - STATE TRACKING
    // ========================================================================

    private float searchTimer;               // Time remaining in search state
    private string currentAction = "Initializing...";  // Current action for debug display

    // ========================================================================
    // PRIVATE FIELDS - WAITING AT WAYPOINTS
    // ========================================================================

    private bool isWaiting = false;          // Is guard waiting at a waypoint?
    private float waitTimer = 0f;            // Time remaining in wait state
    private float minWaitTime = 2f;          // Minimum wait time at waypoint
    private float maxWaitTime = 10f;         // Maximum wait time at waypoint

    // ========================================================================
    // PRIVATE FIELDS - VISION TRACKING
    // ========================================================================

    private bool wasPlayerVisibleLastFrame = false;  // Was player visible in previous frame?
    private float lastVisionCheckTime = 0f;          // Last time vision was checked

    [Header("Guard Navigation")]
    [SerializeField] private float guardAvoidanceRadius = 2f;
    [SerializeField] private float guardPathfindingRadius = 5f;
    [SerializeField] private LayerMask guardLayer;

    // ========================================================================
    // PRIVATE FIELDS - ALERT SYSTEM
    // ========================================================================

    private float lastAlertTime = 0f;        // Last time this guard alerted others
    private bool hasAlertedNearby = false;   // Has alerted nearby guards yet?

    // ========================================================================
    // PUBLIC PROPERTIES
    // ========================================================================

    /// <summary>
    /// Gets the current action description for debugging.
    /// </summary>
    public string GetCurrentAction() => currentAction;

    // ========================================================================
    // PROTECTED OVERRIDES - AIBrain
    // ========================================================================

    protected override void OnPlayerSet()
    {
        Debug.Log($"[BTBrain] Player set - behavior tree will activate");
    }

    // ========================================================================
    // INITIALIZATION METHODS
    // ========================================================================

    /// <summary>
    /// Initializes the BTBrain with a guard reference (no player yet).
    /// Sets up NavMeshAgent, waypoints, anti-collision, and builds behavior tree.
    /// </summary>
    public override void Init(Guard guard)
    {
        this.guard = guard;

        // Set random suspicion time for variety between guards
        SetSuspicionTime(Random.Range(suspicionTimeMin, suspicionTimeMax));

        // Ensure we have the NavMeshAgent component
        SetupNavMeshAgent();

        // Initialize waypoints for patrol behavior
        InitializeWaypoints();

        // Setup anti-collision layer mask if not set
        if (guardLayerMask == 0)
        {
            guardLayerMask = LayerMask.GetMask("Guard", "Default");
        }

        // Setup alert system layer mask if not set
        if (guardLayerForAlert == 0)
        {
            guardLayerForAlert = LayerMask.GetMask("Guard");
        }

        // Initialize anti-collision tracking
        lastPosition = transform.position;
        stuckTimer = 0f;
        isStuck = false;

        // Build the behavior tree structure
        ConstructBehaviourTree();

        hasInitialised = true;
        currentAction = "Patrolling";
    }

    /// <summary>
    /// Initializes the BTBrain with both guard and player references.
    /// Called by SceneSpawner when player exists at spawn time.
    /// </summary>
    public override void Init(Guard guard, Player _player)
    {
        base.Init(guard, _player);

        // Set random suspicion time for variety between guards
        SetSuspicionTime(Random.Range(suspicionTimeMin, suspicionTimeMax));

        //Debug.Log($"[BTBrain] Init with player: {_player?.name}");

        // Ensure we have the NavMeshAgent component
        SetupNavMeshAgent();

        // Initialize waypoints for patrol behavior
        InitializeWaypoints();

        // Setup anti-collision layer mask if not set
        if (guardLayerMask == 0)
        {
            guardLayerMask = LayerMask.GetMask("Guard", "Default");
        }

        // Setup alert system layer mask if not set
        if (guardLayerForAlert == 0)
        {
            guardLayerForAlert = LayerMask.GetMask("Guard");
        }

        // Initialize anti-collision tracking
        lastPosition = transform.position;
        stuckTimer = 0f;
        isStuck = false;

        // Build the behavior tree structure
        ConstructBehaviourTree();

        hasInitialised = true;
        currentAction = "Patrolling";
    }

    /// <summary>
    /// Sets up the NavMeshAgent component and configures initial speed.
    /// </summary>
    private void SetupNavMeshAgent()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = GetComponentInParent<NavMeshAgent>();
            }
        }

        if (agent != null)
        {
            agent.speed = wanderSpeed;
            agent.isStopped = false;
            originalSpeed = wanderSpeed;
        }
    }

    /// <summary>
    /// Initializes waypoints for patrol behavior.
    /// If no waypoints are assigned, uses random wander instead.
    /// </summary>
    private void InitializeWaypoints()
    {
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
            Debug.LogWarning("[BTBrain] No waypoints assigned - using random wander");
        }
    }

    // ========================================================================
    // CORE UPDATE LOOP
    // ========================================================================

    /// <summary>
    /// Main update loop called every frame by the Guard component.
    /// Handles anti-collision, vision/hearing updates, kill checks, and tree processing.
    /// </summary>
    public override void Think()
    {
        if (!hasInitialised || tree == null || guard == null) return;
        if (isStunned) return;

        // Handle anti-collision (prevents guards from stacking)
        HandleAntiCollision();

        // Helps navigate around guards
        AvoidOtherGuardsInPath();        


        // Update vision detection, hearing state, and trigger alerts
        UpdateVisionAndHearing();

        // Check for kill opportunity (if player is in range)
        CheckForKill();

        // Process the behavior tree
        tree.Process();
    }

    // ========================================================================
    // VISION AND HEARING SYSTEM
    // ========================================================================

    /// <summary>
    /// Updates vision detection, hearing state, and triggers GameManager events.
    /// Also triggers the alert system when player is first spotted.
    /// This method matches the FSM's detection logic for consistency.
    /// </summary>
    private void UpdateVisionAndHearing()
    {
        // 1. VISION DETECTION - Use base class multi-point raycast system
        bool canSeePlayer = HasLineOfSightToPlayer();

        // Update guard property for other systems (VisionConeBlocker, etc.)
        if (guard != null)
            guard.CanSeePlayer = canSeePlayer;

        // 2. Handle GameManager events and TRIGGER ALERT SYSTEM
        if (canSeePlayer && !wasPlayerVisibleLastFrame)
        {
            wasPlayerVisibleLastFrame = true;
            lastSeenTime = Time.time;
            lastKnownPlayerPosition = PlayerTransform != null ? PlayerTransform.position : Vector3.zero;

            // Notify GameManager for statistics tracking
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlayerSpotted();
                GameManager.Instance.GuardAlerted();
            }

            Debug.Log($"[BTBrain] VISUAL DETECTION - Player spotted at {lastKnownPlayerPosition}!");
            currentAction = "Chasing";

            // ========== ALERT SYSTEM: Notify nearby guards ==========
            AlertNearbyGuards();
        }
        else if (!canSeePlayer && wasPlayerVisibleLastFrame)
        {
            wasPlayerVisibleLastFrame = false;
            if (GameManager.Instance != null)
                GameManager.Instance.GuardLostPlayer();

            Debug.Log($"[BTBrain] Lost visual on player");
        }

        // 3. Update last known position when player is visible
        if (canSeePlayer && PlayerTransform != null)
        {
            lastKnownPlayerPosition = PlayerTransform.position;
            lastSeenTime = Time.time;
        }

        // 4. HEARING DETECTION - Update suspicious state from base class
        UpdateSuspiciousState();

        // Debug logging for hearing (every 60 frames to reduce spam)
        if (isSuspicious && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[BTBrain HEARING] Suspicious=true, Timer={suspiciousTimer:F1}s, Position={suspiciousPosition}");
        }
    }

    // ========================================================================
    // ALERT SYSTEM (BARK)
    // ========================================================================

    /// <summary>
    /// Alerts nearby guards when this guard spots the player.
    /// Other guards within alertRadius will become suspicious and investigate.
    /// 
    /// FEATURES:
    /// - Cooldown system prevents alert spam (3 seconds between alerts)
    /// - Distance-based reaction delay (closer guards react faster: 0-0.5s delay)
    /// - Compatible with all AI types (BTBrain, FSM, Zombie)
    /// - Suspicion duration scales with distance (closer = longer suspicion)
    /// </summary>
    private void AlertNearbyGuards()
    {
        // Check cooldown to prevent alert spam
        if (Time.time - lastAlertTime < alertCooldown)
        {
            if (debugAlertSystem)
                Debug.Log($"[Alert] {gameObject.name} attempted to alert but on cooldown ({alertCooldown - (Time.time - lastAlertTime):F1}s remaining)");
            return;
        }

        // Find all guards within alert radius using Physics.OverlapSphere
        Collider[] nearbyGuards = Physics.OverlapSphere(transform.position, alertRadius, guardLayerForAlert);

        // Only this guard found - nothing to alert
        if (nearbyGuards.Length <= 1)
        {
            if (debugAlertSystem)
                Debug.Log($"[Alert] {gameObject.name} - no other guards within {alertRadius}m");
            return;
        }

        int alertedCount = 0;

        foreach (Collider col in nearbyGuards)
        {
            // Skip ourselves
            if (col.gameObject == gameObject) continue;

            // Get the guard component from the collider
            Guard otherGuard = col.GetComponent<Guard>();
            if (otherGuard == null) continue;

            // Get the AI brain of the other guard
            AIBrain otherBrain = otherGuard.currentBrain;
            if (otherBrain == null) continue;

            // Calculate distance for priority (closer guards react faster)
            float distance = Vector3.Distance(transform.position, otherGuard.transform.position);
            float reactionDelay = Mathf.Lerp(0f, 0.5f, distance / alertRadius);

            // Alert the other guard with delay based on distance
            StartCoroutine(AlertOtherGuard(otherBrain, reactionDelay, distance));
            alertedCount++;
        }

        lastAlertTime = Time.time;

        if (debugAlertSystem)
        {
            Debug.Log($"[ALERT] {gameObject.name} BARKED - alerted {alertedCount} nearby guards within {alertRadius}m!");
            Debug.Log($"  Alert Position: {transform.position}");
            Debug.Log($"  Last Known Player: {lastKnownPlayerPosition}");
        }
    }

    /// <summary>
    /// Coroutine to alert another guard with a slight delay based on distance.
    /// Simulates sound travel time and gives a more natural, realistic feel.
    /// </summary>
    /// <param name="otherBrain">The AI brain of the guard to alert</param>
    /// <param name="delay">Delay before the alert takes effect (seconds)</param>
    /// <param name="distance">Distance between guards (for logging and scaling)</param>
    private IEnumerator AlertOtherGuard(AIBrain otherBrain, float delay, float distance)
    {
        yield return new WaitForSeconds(delay);

        if (otherBrain == null) yield break;

        if (debugAlertSystem)
        {
            Debug.Log($"[ALERT] {otherBrain.gameObject.name} was alerted by {gameObject.name} (distance: {distance:F1}m) - Reacting after {delay:F1}s delay");
        }

        // Calculate suspicion duration based on distance (closer = longer suspicion)
        float suspicionDuration = Mathf.Lerp(8f, 3f, distance / alertRadius);

        // Handle different brain types appropriately
        if (otherBrain is BTBrain btBrain)
        {
            // For BTBrain: Make them suspicious at the last known player position
            if (lastKnownPlayerPosition != Vector3.zero)
            {
                btBrain.SetSuspicious(lastKnownPlayerPosition, suspicionDuration);
                btBrain.currentAction = "Alerted";
            }
        }
        else if (otherBrain is FSM fsmBrain)
        {
            // For FSM: Set suspicious state to investigate
            if (lastKnownPlayerPosition != Vector3.zero)
            {
                fsmBrain.SetSuspicious(lastKnownPlayerPosition, suspicionDuration);
            }
        }
        else if (otherBrain is Zombie zombieBrain)
        {
            // For Zombie: Make them suspicious/investigate the area
            if (lastKnownPlayerPosition != Vector3.zero)
            {
                zombieBrain.SetSuspicious(lastKnownPlayerPosition, suspicionDuration);
            }
        }
    }

    /// <summary>
    /// Optionally alerts nearby guards when becoming suspicious.
    /// Creates a chain reaction of suspicion spreading through the group.
    /// Only triggers for significant suspicions (duration > 3 seconds).
    /// </summary>
    private IEnumerator AlertNearbyGuardsWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        AlertNearbyGuards();
    }

    // ========================================================================
    // HEARING SYSTEM OVERRIDES
    // ========================================================================

    /// <summary>
    /// Override of SetSuspicious from AIBrain.
    /// Sets the guard to suspicious state and optionally alerts nearby guards.
    /// </summary>
    public override void SetSuspicious(Vector3 position, float duration)
    {
        base.SetSuspicious(position, duration);
        currentAction = "Suspicious";
        //Debug.Log($"[BTBrain] Guard suspicious at {position} from noise, duration: {duration:F1}s");

        // Optionally alert nearby guards when becoming suspicious (chain reaction)
        // Only alert for significant suspicions (duration > 3 seconds)
        // This creates a "spreading suspicion" effect through the group
        if (duration > 3f)
        {
            StartCoroutine(AlertNearbyGuardsWithDelay(0.2f));
        }
    }

    // ========================================================================
    // BEHAVIOR TREE CONDITION METHODS
    // ========================================================================

    /// <summary>
    /// Condition: Is the guard currently suspicious (heard a noise or was alerted)?
    /// Returns SUCCESS if suspicious and timer > 0, otherwise FAILURE.
    /// </summary>
    private Node.Status IsSuspiciousCondition()
    {
        if (isSuspicious && suspiciousTimer > 0)
        {
            return Node.Status.SUCCESS;
        }
        return Node.Status.FAILURE;
    }

    /// <summary>
    /// Action: Investigate suspicious position (from hearing or other guards' alerts).
    /// Moves to the suspicious location, looks around, and checks for player.
    /// </summary>
    private Node.Status SuspiciousAction()
    {
        if (!isSuspicious) return Node.Status.FAILURE;

        suspiciousTimer -= Time.deltaTime;
        currentAction = "Suspicious";

        // Move to suspicious position
        if (agent != null && suspiciousPosition != Vector3.zero)
        {
            agent.isStopped = false;

            // Set destination if we don't have one or reached current target
            if (!agent.hasPath || agent.remainingDistance < 0.5f)
            {
                Vector3 targetPos = new Vector3(suspiciousPosition.x, 0.05f, suspiciousPosition.z);

                if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
            }

            // Look toward destination while moving (realistic head movement)
            if (agent.hasPath && agent.remainingDistance > 1f)
            {
                Vector3 directionToTarget = (agent.destination - transform.position).normalized;
                directionToTarget.y = 0;
                if (directionToTarget != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(directionToTarget);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 60f * Time.deltaTime);
                }
            }
            else if (agent.hasPath && agent.remainingDistance <= 1f)
            {
                // At investigation point, look around to simulate searching
                transform.Rotate(0, 60f * Time.deltaTime, 0);
            }
        }

        // Check if we see the player while suspicious (vision overrides)
        if (HasLineOfSightToPlayer())
        {
            Debug.Log($"[BTBrain] Saw player while suspicious! Switching to chase");
            isSuspicious = false;
            currentAction = "Chasing";
            return Node.Status.FAILURE; // Will make selector try chase sequence
        }

        // Suspicion timer expired - return to patrol
        if (suspiciousTimer <= 0)
        {
            Debug.Log($"[BTBrain] Suspicion ended - returning to patrol");
            isSuspicious = false;
            currentAction = "Patrolling";

            if (agent != null)
            {
                agent.speed = wanderSpeed;
                agent.isStopped = false;
                agent.ResetPath();
            }

            return Node.Status.SUCCESS;
        }

        return Node.Status.RUNNING;
    }

    /// <summary>
    /// Condition: Can the guard see the player?
    /// Uses base class multi-point raycast detection.
    /// </summary>
    private Node.Status CanSeePlayerCondition()
    {
        if (HasLineOfSightToPlayer())
        {
            currentAction = "Chasing";
            return Node.Status.SUCCESS;
        }
        return Node.Status.FAILURE;
    }

    /// <summary>
    /// Condition: Has the guard lost the player and is still searching?
    /// Returns SUCCESS if we have a last known position and search timer > 0.
    /// </summary>
    private Node.Status LostPlayerCondition()
    {
        if (lastKnownPlayerPosition != Vector3.zero && searchTimer > 0)
        {
            currentAction = "Searching";
            return Node.Status.SUCCESS;
        }
        return Node.Status.FAILURE;
    }

    // ========================================================================
    // CHASE BEHAVIOR
    // ========================================================================

    /// <summary>
    /// Chase behavior - moves directly toward the player at chase speed.
    /// Updates last known position for search behavior.
    /// </summary>
    public override void Chase(Transform target)
    {
        if (target != null && agent != null)
        {
            agent.speed = chaseSpeed;
            Seek(target.position);
        }
    }

    /// <summary>
    /// Action: Chase the player.
    /// If player is lost during chase, transitions to search state.
    /// </summary>
    private Node.Status ChasePlayerAction()
    {
        if (PlayerTransform == null) return Node.Status.FAILURE;

        Chase(PlayerTransform);
        lastKnownPlayerPosition = PlayerTransform.position;

        // Lost sight of player - start searching at last known position
        if (!HasLineOfSightToPlayer())
        {
            searchTimer = searchDuration;
            currentAction = "Searching";
            Debug.Log($"[BTBrain] Lost player during chase - searching at last known position");
            return Node.Status.SUCCESS; // Move to search
        }

        return Node.Status.RUNNING;
    }

    // ========================================================================
    // SEARCH BEHAVIOR
    // ========================================================================

    /// <summary>
    /// Action: Search the last known player position.
    /// Moves to random points around the last seen location.
    /// </summary>
    private Node.Status SearchAction()
    {
        if (lastKnownPlayerPosition == Vector3.zero || agent == null)
            return Node.Status.FAILURE;

        searchTimer -= Time.deltaTime;

        // Found player during search - return to chase
        if (HasLineOfSightToPlayer())
        {
            searchTimer = 0;
            currentAction = "Chasing";
            //Debug.Log($"[BTBrain] Found player during search!");
            return Node.Status.SUCCESS; // Found player, go to chase
        }

        // Move to a random point around the last known position
        if (!agent.hasPath || agent.remainingDistance < 0.5f)
        {
            Vector3 searchPosition = lastKnownPlayerPosition + Random.insideUnitSphere * 5f;
            searchPosition.y = lastKnownPlayerPosition.y;

            if (NavMesh.SamplePosition(searchPosition, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                Seek(hit.position);
                //Debug.Log($"[BTBrain] Searching area: {hit.position}");
            }
        }

        // Search time expired - give up and return to patrol
        if (searchTimer <= 0)
        {
            lastKnownPlayerPosition = Vector3.zero;
            currentAction = "Patrolling";
            //Debug.Log($"[BTBrain] Search finished - returning to patrol");
            return Node.Status.SUCCESS; // Search done, go to patrol
        }

        return Node.Status.RUNNING;
    }

    // ========================================================================
    // PATROL BEHAVIOR
    // ========================================================================

    /// <summary>
    /// Wander/Patrol behavior - moves between waypoints or random locations.
    /// Uses waypoints if assigned, otherwise random wander.
    /// </summary>
    public override void Wander()
    {
        if (agent == null) return;

        agent.speed = wanderSpeed;

        if (!agent.hasPath || agent.remainingDistance < 0.5f)
        {
            if (waypoints != null && waypoints.Length > 0)
            {
                // Use waypoint patrol (cycle through array)
                Transform targetWaypoint = waypoints[currentWaypointIndex];
                if (targetWaypoint != null)
                {
                    Seek(targetWaypoint.position);
                    currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                }
            }
            else
            {
                // Random wander when no waypoints assigned
                Vector3 randomDirection = Random.insideUnitSphere * 10f;
                randomDirection += transform.position;

                if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, 10, NavMesh.AllAreas))
                {
                    Seek(hit.position);
                }
            }
        }
    }

    /// <summary>
    /// Action: Patrol behavior.
    /// Checks for player while patrolling and handles waypoint waiting.
    /// </summary>
    private Node.Status PatrolAction()
    {
        Wander();

        // Check for player while patrolling (vision overrides patrol)
        if (HasLineOfSightToPlayer())
        {
            currentAction = "Chasing";
            Debug.Log($"[BTBrain] Saw player while patrolling!");
            return Node.Status.FAILURE;
        }

        // Check if reached a waypoint and should start waiting
        if (agent != null && !agent.pathPending && agent.remainingDistance < 0.5f && !isWaiting)
        {
            if (waypoints != null && waypoints.Length > 0)
            {
                isWaiting = true;
                waitTimer = Random.Range(minWaitTime, maxWaitTime);
                Debug.Log($"[BTBrain] Reached waypoint {currentWaypointIndex}. Waiting for {waitTimer:F1} seconds...");
            }
        }

        return Node.Status.RUNNING;
    }

    // ========================================================================
    // WAYPOINT WAITING BEHAVIOR
    // ========================================================================

    /// <summary>
    /// Condition: Is the guard currently waiting at a waypoint?
    /// </summary>
    private Node.Status IsWaitingCondition()
    {
        return (isWaiting && waitTimer > 0) ? Node.Status.SUCCESS : Node.Status.FAILURE;
    }

    /// <summary>
    /// Action: Wait at a waypoint.
    /// Stops moving, looks around, then continues to next waypoint.
    /// Creates more natural patrol behavior.
    /// </summary>
    private Node.Status WaitAction()
    {
        if (!isWaiting) return Node.Status.FAILURE;

        waitTimer -= Time.deltaTime;
        currentAction = "Waiting";

        // Stop moving while waiting
        if (agent != null && agent.hasPath)
        {
            agent.ResetPath();
        }

        // Look around while waiting (creepy head-turning effect)
        transform.Rotate(0, 10 * Time.deltaTime, 0);

        // Wait time finished - move to next waypoint
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
                    Debug.Log($"[BTBrain] Moving to waypoint {currentWaypointIndex} after waiting");
                }
            }

            return Node.Status.SUCCESS;
        }

        return Node.Status.RUNNING;
    }

    // ========================================================================
    // ANTI-COLLISION SYSTEM
    // ========================================================================

    /// <summary>
    /// Prevents guards from stacking on top of each other.
    /// Detects nearby guards and applies avoidance force.
    /// Also detects when guard is stuck and forces a reposition.
    /// </summary>
    private void HandleAntiCollision()
    {
        if (agent == null || !agent.isActiveAndEnabled) return;

        // Check if stuck (not moving for stuckCheckTime seconds)
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        if (distanceMoved < stuckDistanceThreshold)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckCheckTime && !isStuck)
            {
                isStuck = true;
                //Debug.Log($"[BTBrain] {gameObject.name} is stuck! Applying avoidance force.");
            }
        }
        else
        {
            stuckTimer = 0f;
            isStuck = false;
        }
        lastPosition = transform.position;

        // Find nearby guards within anti-collision radius
        Collider[] nearbyGuards = Physics.OverlapSphere(transform.position, antiCollisionRadius, guardLayerMask);
        antiCollisionForce = Vector3.zero;

        foreach (Collider col in nearbyGuards)
        {
            if (col.gameObject == gameObject) continue;

            Guard otherGuard = col.GetComponent<Guard>();
            if (otherGuard == null) continue;

            // Calculate avoidance force (push away from other guard)
            Vector3 directionAway = transform.position - col.transform.position;
            float distance = directionAway.magnitude;

            if (distance < antiCollisionRadius)
            {
                float strength = antiCollisionStrength * (1f - (distance / antiCollisionRadius));
                antiCollisionForce += directionAway.normalized * strength;
            }
        }

        // Apply anti-collision force (move away from nearby guards)
        if (antiCollisionForce != Vector3.zero)
        {
            antiCollisionTimer = 0.5f;
            Vector3 newPosition = transform.position + antiCollisionForce * Time.deltaTime;

            if (NavMesh.SamplePosition(newPosition, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }

            Debug.DrawRay(transform.position, antiCollisionForce, Color.red);
        }

        // Force reposition if stuck (prevents infinite waiting)
        if (isStuck && antiCollisionTimer <= 0)
        {
            Vector3 randomDirection = Random.insideUnitSphere * 2f;
            randomDirection.y = 0;

            if (NavMesh.SamplePosition(transform.position + randomDirection, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                agent.ResetPath();
                Debug.Log($"[BTBrain] {gameObject.name} was stuck - forced reposition");
            }
            isStuck = false;
        }

        antiCollisionTimer -= Time.deltaTime;
    }

    private void AvoidOtherGuardsInPath()
    {
        if (agent == null || !agent.hasPath) return;

        // Find nearby guards that might be blocking the path
        Collider[] nearbyGuards = Physics.OverlapSphere(transform.position, guardPathfindingRadius, guardLayer);

        foreach (Collider col in nearbyGuards)
        {
            if (col.gameObject == gameObject) continue;

            Guard otherGuard = col.GetComponent<Guard>();
            if (otherGuard == null) continue;

            float distance = Vector3.Distance(transform.position, otherGuard.transform.position);

            // If another guard is too close and in our path, adjust destination slightly
            if (distance < guardAvoidanceRadius)
            {
                // Check if the other guard is between us and our destination
                Vector3 toDestination = agent.destination - transform.position;
                Vector3 toOtherGuard = otherGuard.transform.position - transform.position;

                float angleToOther = Vector3.Angle(toDestination, toOtherGuard);

                // If other guard is in front of us (within 45 degrees)
                if (angleToOther < 45f)
                {
                    // Adjust destination to go around
                    Vector3 perpendicular = Vector3.Cross(Vector3.up, toOtherGuard).normalized;
                    Vector3 newDestination = agent.destination + perpendicular * 2f;

                    if (NavMesh.SamplePosition(newDestination, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                    {
                        agent.SetDestination(hit.position);
                        Debug.DrawLine(transform.position, hit.position, Color.magenta, 0.5f);
                    }
                }
            }
        }
    }


    // ========================================================================
    // BEHAVIOR TREE CONSTRUCTION
    // ========================================================================

    /// <summary>
    /// Builds the behavior tree structure.
    /// Priority order (from highest to lowest):
    /// 1. Suspicious - Investigate noises or alerts
    /// 2. Chase - Chase visible player
    /// 3. Search - Search last known position
    /// 4. Wait - Pause at waypoints
    /// 5. Patrol - Default wander behavior
    /// </summary>
    private void ConstructBehaviourTree()
    {
        tree = new BehaviourTree("BTBrain Tree");
        Selector root = new Selector("Root Selector");

        // 1. SUSPICIOUS (highest priority) - HEARING DETECTION & ALERTS
        Sequence suspiciousSequence = new Sequence("Suspicious Sequence");
        suspiciousSequence.AddChild(new Leaf("Is Suspicious?", IsSuspiciousCondition));
        suspiciousSequence.AddChild(new Leaf("Investigate", SuspiciousAction));

        // 2. CHASE BEHAVIOR - VISION DETECTION
        Sequence chaseSequence = new Sequence("Chase Sequence");
        chaseSequence.AddChild(new Leaf("Can See Player?", CanSeePlayerCondition));
        chaseSequence.AddChild(new Leaf("Chase Player", ChasePlayerAction));

        // 3. SEARCH BEHAVIOR - LOST PLAYER
        Sequence searchSequence = new Sequence("Search Sequence");
        searchSequence.AddChild(new Leaf("Lost Player?", LostPlayerCondition));
        searchSequence.AddChild(new Leaf("Search at Last Position", SearchAction));

        // 4. WAIT BEHAVIOR - WAYPOINT WAITING
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

    // ========================================================================
    // DEBUG VISUALIZATION
    // ========================================================================

    /// <summary>
    /// Draws debug gizmos in the Scene view.
    /// Shows: current action, destination, waypoints, search area, suspicious position, and alert radius.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !hasInitialised || agent == null)
            return;

        Vector3 textPos = transform.position + Vector3.up * 2.5f;

#if UNITY_EDITOR
        // Current action label (white)
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(textPos, $"BT: {currentAction}");

        // Destination label (below action)
        if (agent.hasPath && agent.destination != Vector3.zero)
        {
            UnityEditor.Handles.Label(textPos + Vector3.down * 0.5f, $"Dest: {agent.destination}");
        }
#endif

        // Draw line to destination (cyan)
        if (agent.hasPath && agent.destination != Vector3.zero)
        {
            Debug.DrawLine(transform.position, agent.destination, Color.cyan);
        }

        // Draw waypoints (cyan spheres)
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

        // Draw last known player position when searching (yellow)
        if (currentAction == "Searching" && lastKnownPlayerPosition != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lastKnownPlayerPosition, 1f);
            Debug.DrawLine(transform.position, lastKnownPlayerPosition, Color.yellow);
        }

        // Draw search timer label (yellow)
        if (searchTimer > 0)
        {
            Vector3 timerPos = transform.position + Vector3.up * 2f;
#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.Label(timerPos, $"Search: {searchTimer:F1}s");
#endif
        }

        // Draw suspicious position (magenta)
        if (isSuspicious && suspiciousPosition != Vector3.zero)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(suspiciousPosition, 1f);
            Debug.DrawLine(transform.position, suspiciousPosition, Color.magenta);
        }

        // Draw ALERT RADIUS (orange) for debugging
        if (showAlertRadius && debugAlertSystem)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);  // Semi-transparent orange
            Gizmos.DrawWireSphere(transform.position, alertRadius);

#if UNITY_EDITOR
            UnityEditor.Handles.color = new Color(1f, 0.5f, 0f, 0.8f);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3.5f,
                $"ALERT RADIUS: {alertRadius}m");
#endif
        }
    }
}