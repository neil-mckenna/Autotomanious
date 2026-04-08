using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// ============================================================================
// AIBRAIN - ABSTRACT BASE CLASS FOR ALL ARTIFICIAL INTELLIGENCE SYSTEMS
// ============================================================================
// 
// This is the foundation for all AI behaviors in the game. It provides:
// 1. Vision detection (multi-point raycast with persistence)
// 2. Hearing detection (noise-based suspicion)
// 3. Status effects (stun, blind, slow)
// 4. Kill logic (range + line of sight check)
// 5. Waypoint patrol system
// 6. Suspicion and investigation behavior
//
// DERIVED CLASSES:
// - FSM: Finite State Machine based AI (Patrol -> Suspicious -> Chase -> Search)
// - BTBrain: Behavior Tree based AI (modular, reusable behavior nodes)
// - Zombie: Specialized AI with unique movement and sound behavior
//
// ============================================================================

public abstract class AIBrain : MonoBehaviour
{
    // ========================================================================
    // SERIALIZED FIELDS - CONFIGURE IN UNITY INSPECTOR
    // ========================================================================

    [Header("=== AIBRAIN CORE SETTINGS ===")]
    [Tooltip("Reference to the NavMeshAgent for movement")]
    [SerializeField] protected NavMeshAgent agent;

    [Tooltip("Array of waypoints for patrol behavior")]
    [SerializeField] protected Transform[] waypoints;

    // ========================================================================
    // VISION DETECTION FIELDS (Multi-Point Raycast System)
    // ========================================================================

    // Stores timestamps for when each body part was last successfully raycast
    // Creates persistence - if player was partially visible recently, they stay detected
    private float[] hitTimestamps = new float[5];

    [Tooltip("How long a hit 'counts' toward detection (seconds)")]
    private float detectionWindow = 0.5f;

    [Tooltip("Minimum number of body parts that must be visible for detection")]
    private int requiredHits = 2;

    private bool wasDetectedLastFrame = false;

    // ========================================================================
    // GUARD REFERENCE
    // ========================================================================

    protected Guard Guard => guard;  // Public property for derived classes
    protected Guard guard;           // Reference to the Guard component

    // ========================================================================
    // PLAYER REFERENCE WITH AUTO-FIND
    // ========================================================================

    protected Player player;

    /// <summary>
    /// Gets the player reference, automatically finding it if null.
    /// This lazy-loading pattern ensures the AI always has a player target.
    /// </summary>
    public Player Player
    {
        get
        {
            if (player == null)
            {
                FindPlayer();
            }
            return player;
        }
        set
        {
            player = value;
            if (player != null)
            {
                Debug.Log($"{GetType().Name}: Player manually set to {player.name}");
            }
        }
    }

    /// <summary>
    /// Sets the player reference manually (called by SceneSpawner).
    /// </summary>
    public void SetPlayer(Player newPlayer)
    {
        player = newPlayer;
        OnPlayerSet();
        Debug.Log($"{GetType().Name}: Player reference set to {(player != null ? player.name : "null")}");
    }

    /// <summary>
    /// Called after player is set - override for initialization that needs player.
    /// </summary>
    protected virtual void OnPlayerSet() { }

    /// <summary>
    /// Attempts to get player reference, returns success/failure.
    /// Prevents null reference exceptions when checking player.
    /// </summary>
    protected bool TryGetPlayer(out Player playerRef)
    {
        if (player != null)
        {
            playerRef = player;
            return true;
        }

        if (GameManager.Instance != null)
        {
            player = GameManager.Instance.GetPlayer();
            if (player != null)
            {
                playerRef = player;
                return true;
            }
        }

        playerRef = null;
        return false;
    }

    // ========================================================================
    // PATROL TRACKING
    // ========================================================================

    protected int currentWaypointIndex = 0;      // Current waypoint in patrol route
    protected Vector3 lastKnownPlayerPosition;   // Last seen/heard player position

    // ========================================================================
    // STATUS EFFECTS (Stun, Blind, Slow)
    // ========================================================================

    protected bool isStunned = false;
    protected bool isBlinded = false;
    protected bool isSlowed = false;

    protected float originalSpeed;              // Store original speed for restoration
    protected float originalDetectionRange;    // Store original detection range for blind effect

    protected Coroutine stunCoroutine;
    protected Coroutine blindCoroutine;
    protected Coroutine slowCoroutine;

    // ========================================================================
    // SUSPICION SYSTEM (For hearing detection)
    // ========================================================================

    protected bool isSuspicious = false;        // Is AI suspicious of something?
    protected Vector3 suspiciousPosition;       // Position to investigate
    protected float suspiciousTimer;            // Time remaining in suspicious state
    protected float suspicionDuration = 3f;     // Total duration of suspicion

    // ========================================================================
    // KILL TRACKING
    // ========================================================================

    protected bool hasKilledPlayer = false;     // Prevents multiple kills

    // ========================================================================
    // CHASE TRACKING
    // ========================================================================

    protected float lastSeenTime = 0f;          // Last time player was seen
    protected bool wasPlayerVisible = false;    // Was player visible last frame?

    // ========================================================================
    // PUBLIC PROPERTIES
    // ========================================================================

    public NavMeshAgent Agent => agent;
    public bool IsStunned => isStunned;
    public bool IsBlinded => isBlinded;
    public bool IsSlowed => isSlowed;
    public bool IsSuspicious => isSuspicious;

    /// <summary>
    /// Kill range with safety clamping to prevent absurd values.
    /// </summary>
    public float KillRange => guard != null ? Mathf.Clamp(guard.GetKillRange(), 0.5f, 3f) : 1.5f;

    protected Transform PlayerTransform => player != null ? player.transform : null;

    // ========================================================================
    // INITIALIZATION METHODS
    // ========================================================================

    /// <summary>
    /// Sets the NavMeshAgent reference and stores original speed.
    /// </summary>
    public void SetAgent(NavMeshAgent navAgent)
    {
        agent = navAgent;
        if (agent != null)
            originalSpeed = agent.speed;
    }

    /// <summary>
    /// Finds the player in the scene (called automatically when Player property is accessed).
    /// Uses recursive retry if player not found immediately.
    /// </summary>
    protected virtual void FindPlayer()
    {
        if (player != null) return;

        player = FindAnyObjectByType<Player>();
        if (player != null)
        {
            //Debug.Log($"{GetType().Name}: Auto-found player at {player.transform.position}");
        }
        else
        {
            // Retry every 0.5 seconds until player is found
            Invoke(nameof(FindPlayer), 0.5f);
        }
    }

    public bool HasPlayer() => Player != null;

    // ========================================================================
    // WAYPOINT SYSTEM
    // ========================================================================

    /// <summary>
    /// Sets the waypoints for patrol behavior.
    /// </summary>
    public void SetWaypoints(Transform[] newWaypoints)
    {
        waypoints = newWaypoints;
    }

    /// <summary>
    /// Gets the next waypoint in the patrol route (cycles through array).
    /// </summary>
    public Transform GetNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return null;
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        return waypoints[currentWaypointIndex];
    }

    // ========================================================================
    // SUSPICION SETTINGS
    // ========================================================================

    [SerializeField] private float suspicionTime = 3f;

    public void SetSuspicionTime(float seconds)
    {
        suspicionTime = Mathf.Clamp(seconds, 1f, 20f);
        //Debug.Log($"{GetType().Name}: Suspicion time set to {suspicionTime} seconds");
    }

    public float GetSuspicionTime() => suspicionTime;

    // ========================================================================
    // VISION DETECTION SYSTEM
    // ========================================================================
    // 
    // HOW IT WORKS:
    // 1. Checks if player is within detection range and field of view
    // 2. Raycasts to 5 different body parts (feet to head)
    // 3. Tracks hits over time using timestamps (0.5 second window)
    // 4. Requires 2+ body parts to be visible for detection
    // 5. Provides persistence - player remains detected briefly after losing sight
    //
    // This prevents:
    // - Detection through tiny gaps (need multiple body parts)
    // - Flickering detection (timestamp persistence)
    // - Unfair detection when only a hand is visible
    //
    // =========================================================================

    /// <summary>
    /// Gets the eye position for raycast origin (uses Guard's raycast start if available).
    /// </summary>
    protected virtual Vector3 GetEyePosition()
    {
        return guard.RayCastStartLocation != null
            ? guard.RayCastStartLocation.position
            : transform.position + Vector3.up * 1f;
    }

    /// <summary>
    /// Gets the body part offsets for multi-point detection.
    /// Can be overridden for different-sized enemies.
    /// </summary>
    protected virtual Vector3[] GetTargetOffsets()
    {
        return new Vector3[]
        {
            Vector3.up * 0.3f,  // Feet
            Vector3.up * 0.7f,  // Hips
            Vector3.up * 1.0f,  // Waist
            Vector3.up * 1.3f,  // Chest
            Vector3.up * 1.6f   // Head
        };
    }

    /// <summary>
    /// Advanced detection method that returns where the player was detected and when.
    /// Uses multi-point raycast with temporal persistence.
    /// </summary>
    public virtual bool TryGetPlayerDetection(out Vector3 detectedPosition, out float detectionTime)
    {
        detectedPosition = Vector3.zero;
        detectionTime = 0f;

        if (player == null || guard == null) return false;

        Vector3 eyePosition = GetEyePosition();
        float detectionRange = guard.GetDetectionRange();
        float halfFOV = guard.GetFieldOfView() * 0.5f;

        // Quick distance and angle checks (cheap rejection)
        Vector3 playerCenter = player.transform.position + Vector3.up * 1.0f;
        Vector3 toPlayer = playerCenter - eyePosition;
        float distance = toPlayer.magnitude;
        float angle = Vector3.Angle(transform.forward, toPlayer);

        if (distance > detectionRange) return false;
        if (angle > halfFOV) return false;

        // Multi-point raycast check
        Vector3[] checkPoints = new Vector3[]
        {
            player.transform.position + Vector3.up * 0.2f,
            player.transform.position + Vector3.up * 0.8f,
            player.transform.position + Vector3.up * 1.2f,
            player.transform.position + Vector3.up * 1.5f,
            player.transform.position + Vector3.up * 1.8f
        };

        int currentHits = 0;
        float currentTime = Time.time;
        Vector3 averageHitPosition = Vector3.zero;

        for (int i = 0; i < checkPoints.Length; i++)
        {
            // Clear old timestamps outside detection window
            if (currentTime - hitTimestamps[i] > detectionWindow)
            {
                hitTimestamps[i] = 0f;
            }

            // Raycast to this body part
            Vector3 direction = (checkPoints[i] - eyePosition).normalized;
            float checkDistance = Vector3.Distance(eyePosition, checkPoints[i]);

            if (Physics.Raycast(eyePosition, direction, out RaycastHit hit, checkDistance))
            {
                if (hit.transform.root == player.transform)
                {
                    hitTimestamps[i] = currentTime;
                    currentHits++;
                    averageHitPosition += checkPoints[i];
                    Debug.DrawLine(eyePosition, checkPoints[i], Color.green, 0.1f);
                }
                else
                {
                    Debug.DrawLine(eyePosition, hit.point, Color.red, 0.1f);
                }
            }
            // Count recent hits from timestamps (persistence)
            else if (hitTimestamps[i] > 0)
            {
                currentHits++;
                averageHitPosition += checkPoints[i];
            }
        }

        if (currentHits > 0)
        {
            averageHitPosition /= currentHits;
        }

        bool isDetected = currentHits >= requiredHits;

        if (isDetected)
        {
            detectedPosition = averageHitPosition;
            detectionTime = currentTime;

            if (!wasDetectedLastFrame)
            {
                //Debug.Log($"[DETECTION] Player spotted at {detectedPosition} with {currentHits} hits!");
            }
        }

        wasDetectedLastFrame = isDetected;
        return isDetected;
    }

    /// <summary>
    /// Simple line-of-sight check (backward compatible).
    /// </summary>
    public virtual bool HasLineOfSightToPlayer()
    {
        return TryGetPlayerDetection(out _, out _);
    }

    public virtual bool CanSeePlayer()
    {
        return HasLineOfSightToPlayer();
    }

    /// <summary>
    /// Updates player visibility and triggers GameManager events.
    /// Called by derived classes to handle spotted/lost events.
    /// </summary>
    protected virtual void UpdatePlayerVisibility()
    {
        if (player == null) return;

        bool canSee = HasLineOfSightToPlayer();

        if (guard != null)
            guard.CanSeePlayer = canSee;

        if (canSee && !wasPlayerVisible)
        {
            wasPlayerVisible = true;
            lastSeenTime = Time.time;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlayerSpotted();
            }
        }
        else if (!canSee && wasPlayerVisible)
        {
            wasPlayerVisible = false;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.GuardLostPlayer();
            }
        }
    }

    // ========================================================================
    // HEARING DETECTION SYSTEM
    // ========================================================================
    //
    // HOW IT WORKS:
    // 1. Receives noise event from Player.MakeNoise()
    // 2. Distance is pre-calculated by Player for efficiency
    // 3. Checks if noise is within hearing range (modified by volume)
    // 4. If player is visible, ignores noise (vision takes priority)
    // 5. Calculates suspicion duration based on distance and volume
    // 6. Sets suspicious state to investigate the noise position
    //
    // =========================================================================

    /// <summary>
    /// Handles noise detection from the player.
    /// Volume affects both hearing range and suspicion duration.
    /// </summary>
    public virtual void HearNoise(Vector3 noisePosition, float distanceToNoise, float noiseRadius, string noiseType, float volume = 1f)
    {
        //Debug.Log($"<color=red>[HEARING CALLED] {gameObject.name}</color>");
        //Debug.Log($"  Received distance (pre-calculated): {distanceToNoise:F2}m");
        //Debug.Log($"  Noise Position: {noisePosition}");
        //Debug.Log($"  My Position: {transform.position}");

        if (isStunned) return;

        float maxHearingRange = guard.GetMaxHearingDistance();
        float effectiveRange = maxHearingRange * Mathf.Clamp(volume, 0.3f, 1.5f);

        if (distanceToNoise > effectiveRange)
        {
            //Debug.Log($"  Too far! ({distanceToNoise:F2}m > {effectiveRange:F2}m)");
            return;
        }

        if (distanceToNoise > noiseRadius * 1.5f)
        {
            //Debug.Log($"  Outside noise radius influence zone");
            return;
        }

        // Vision overrides hearing - if we can see the player, ignore the noise
        if (HasLineOfSightToPlayer())
        {
            //Debug.Log($"  Heard noise but CAN SEE player - ignoring");
            return;
        }

        // Calculate suspicion duration based on distance and volume
        float distanceFactor = 1f - (distanceToNoise / effectiveRange);
        float volumeFactor = Mathf.Clamp(volume, 0.5f, 1.5f);
        float suspicionDuration = GetSuspicionTime() * distanceFactor * volumeFactor;
        suspicionDuration = Mathf.Max(suspicionDuration, 1.5f);

        //Debug.Log($"  HEARD! Suspicion duration: {suspicionDuration:F1}s");
        //Debug.Log($"  Investigating: {noisePosition}");

        SetSuspicious(noisePosition, suspicionDuration);
    }

    // ========================================================================
    // KILL LOGIC
    // ========================================================================
    //
    // SAFETY CHECKS:
    // 1. Distance check - must be within KillRange
    // 2. Line of sight check - cannot kill through walls
    // 3. Double validation in KillPlayer to prevent bugs
    //
    // =========================================================================

    protected virtual bool IsPlayerInKillRange()
    {
        if (Player == null || guard == null) return false;
        float distanceToPlayer = Vector3.Distance(transform.position, Player.transform.position);
        return distanceToPlayer <= KillRange;
    }

    /// <summary>
    /// Checks if player is in kill range and has line of sight.
    /// Called automatically in Think() methods.
    /// </summary>
    protected virtual void CheckForKill()
    {
        if (hasKilledPlayer) return;
        if (guard == null) return;

        Player currentPlayer = this.Player;
        if (currentPlayer == null) return;

        float distanceToPlayer = Vector3.Distance(guard.transform.position, currentPlayer.transform.position);

        if (distanceToPlayer > KillRange) return;

        // Line of sight check - prevents killing through walls
        Vector3 eyePosition = GetEyePosition();
        Vector3 targetPosition = currentPlayer.transform.position + Vector3.up * 1f;
        Vector3 direction = targetPosition - eyePosition;

        if (Physics.Raycast(eyePosition, direction, out RaycastHit hit, distanceToPlayer))
        {
            if (hit.transform.root == currentPlayer.transform)
            {
                Debug.Log($"<color=red>KILL! Distance: {distanceToPlayer:F2}m, Direct line of sight</color>");
                KillPlayer(KillRange.ToString(), currentPlayer);
            }
            else
            {
                //Debug.Log($"[KILL] Blocked by {hit.transform.name} - Can't kill through walls!");
            }
        }
        else
        {
            //Debug.Log($"[KILL] No line of sight to player");
        }
    }

    /// <summary>
    /// Kills the player with safety checks to prevent bugs.
    /// </summary>
    public virtual void KillPlayer(string killRange, Player playerToKill = null)
    {
        if (hasKilledPlayer) return;

        Player targetPlayer = playerToKill != null ? playerToKill : this.Player;

        if (guard != null && targetPlayer != null)
        {
            float actualDistance = Vector3.Distance(guard.transform.position, targetPlayer.transform.position);
            float currentKillRange = KillRange;

            Debug.Log($"[KILLPLAYER] Final check - Distance: {actualDistance:F2}m | Range: {currentKillRange:F2}m | Player: {targetPlayer.name}");

            // Safety check - prevent kills from outside range (bug prevention)
            if (actualDistance > currentKillRange + 0.1f)
            {
                Debug.LogError($"<color=red>BUG PREVENTED: Kill attempted from {actualDistance:F2}m but range is {currentKillRange:F2}m!</color>");
                return;
            }
        }

        hasKilledPlayer = true;
        Debug.LogWarning($"<color=red>!!! {GetType().Name} KILLED PLAYER !!!</color>");

        if (targetPlayer != null)
        {
            targetPlayer.Die(gameObject.name, gameObject);
        }
    }

    // ========================================================================
    // SUSPICIOUS BEHAVIOR
    // ========================================================================

    /// <summary>
    /// Sets the AI to suspicious state, investigating a position.
    /// Adds random offset to investigation point to make behavior less predictable.
    /// </summary>
    public virtual void SetSuspicious(Vector3 position, float duration)
    {
        // Add random offset to investigation point (6-12 meter radius)
        float randomRadius = Random.Range(6f, 12f);
        Vector2 randomCircle = Random.insideUnitCircle * randomRadius;

        Vector3 investigationPoint = new Vector3(
            position.x + randomCircle.x,
            0.05f,
            position.z + randomCircle.y
        );

        suspiciousPosition = investigationPoint;
        isSuspicious = true;
        suspiciousTimer = duration;
        suspicionDuration = duration;

        Debug.Log($"<color=yellow>[SUSPICIOUS] Noise at {position}</color>");
        //Debug.Log($"  Guard will search at: {investigationPoint} (+-{randomRadius:F1}m)");
    }

    /// <summary>
    /// Updates the suspicious timer - call this in Think().
    /// </summary>
    protected virtual void UpdateSuspiciousState()
    {
        if (isSuspicious)
        {
            suspiciousTimer -= Time.deltaTime;
            if (suspiciousTimer <= 0)
            {
                isSuspicious = false;
            }
        }
    }

    protected virtual void ResetSuspicious()
    {
        isSuspicious = false;
        suspiciousTimer = 0f;
    }

    /// <summary>
    /// Moves the AI to investigate the suspicious position.
    /// Can be overridden for different investigation behavior.
    /// </summary>
    protected virtual void InvestigateSuspiciousPosition()
    {
        if (!isSuspicious) return;

        if (agent != null && (!agent.hasPath || agent.remainingDistance < 1.0f))
        {
            Vector3 randomOffset = Random.insideUnitSphere * 5f;
            randomOffset.y = 0;
            Seek(suspiciousPosition + randomOffset);
        }
    }

    // ========================================================================
    // STATUS EFFECTS (Stun, Blind, Slow)
    // ========================================================================

    /// <summary>
    /// Stuns the AI - stops all movement for duration.
    /// </summary>
    public virtual void Stun(float duration)
    {
        if (isStunned) return;
        if (stunCoroutine != null) StopCoroutine(stunCoroutine);
        stunCoroutine = StartCoroutine(StunCoroutine(duration));
    }

    protected virtual IEnumerator StunCoroutine(float duration)
    {
        isStunned = true;
        float storedSpeed = agent.speed;
        agent.speed = 0f;
        agent.isStopped = true;
        agent.ResetPath();
        ApplyStunVisual();
        yield return new WaitForSeconds(duration);
        agent.speed = storedSpeed;
        agent.isStopped = false;
        RemoveStunVisual();
        isStunned = false;
    }

    /// <summary>
    /// Blinds the AI - reduces detection range for duration.
    /// </summary>
    public virtual void Blind(float duration, float blindFactor = 0.1f)
    {
        if (isBlinded) return;
        if (blindCoroutine != null) StopCoroutine(blindCoroutine);
        blindCoroutine = StartCoroutine(BlindCoroutine(duration, blindFactor));
    }

    protected virtual IEnumerator BlindCoroutine(float duration, float blindFactor)
    {
        isBlinded = true;
        originalDetectionRange = guard.GetDetectionRange();
        guard.SetDetectionRange(originalDetectionRange * blindFactor);
        ApplyBlindVisual();
        yield return new WaitForSeconds(duration);
        guard.SetDetectionRange(originalDetectionRange);
        RemoveBlindVisual();
        isBlinded = false;
    }

    /// <summary>
    /// Slows the AI - reduces movement speed for duration.
    /// </summary>
    public virtual void Slow(float duration, float speedMultiplier = 0.5f)
    {
        if (isStunned) return;
        if (slowCoroutine != null) StopCoroutine(slowCoroutine);
        slowCoroutine = StartCoroutine(SlowCoroutine(duration, speedMultiplier));
    }

    protected virtual IEnumerator SlowCoroutine(float duration, float speedMultiplier)
    {
        isSlowed = true;
        float storedSpeed = agent.speed;
        agent.speed = storedSpeed * speedMultiplier;
        ApplySlowVisual();
        yield return new WaitForSeconds(duration);
        agent.speed = storedSpeed;
        RemoveSlowVisual();
        isSlowed = false;
    }

    /// <summary>
    /// Restores all status effects (stun, blind, slow) immediately.
    /// </summary>
    public virtual void RestoreAllEffects()
    {
        if (stunCoroutine != null) StopCoroutine(stunCoroutine);
        if (blindCoroutine != null) StopCoroutine(blindCoroutine);
        if (slowCoroutine != null) StopCoroutine(slowCoroutine);

        if (agent != null)
        {
            agent.speed = originalSpeed;
            agent.isStopped = false;
        }

        if (guard != null && originalDetectionRange > 0)
        {
            guard.SetDetectionRange(originalDetectionRange);
        }

        RemoveStunVisual();
        RemoveBlindVisual();
        RemoveSlowVisual();

        isStunned = false;
        isBlinded = false;
        isSlowed = false;
    }

    // ========================================================================
    // VISUAL FEEDBACK (Override for different enemy types)
    // ========================================================================

    protected virtual void ApplyStunVisual()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = Color.yellow;
    }

    protected virtual void RemoveStunVisual()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = Color.white;
    }

    protected virtual void ApplyBlindVisual()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = Color.gray;
    }

    protected virtual void RemoveBlindVisual()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = Color.white;
    }

    protected virtual void ApplySlowVisual()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = Color.cyan;
    }

    protected virtual void RemoveSlowVisual()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = Color.white;
    }

    /// <summary>
    /// Flashes the AI red (for damage feedback).
    /// </summary>
    public virtual void FlashRedForDuration(float flashDuration = 0.3f, int flashes = 3)
    {
        StartCoroutine(FlashRedCoroutine(flashDuration, flashes));
    }

    protected virtual IEnumerator FlashRedCoroutine(float flashDuration, int flashes)
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null) yield break;
        Color originalColor = renderer.material.color;
        for (int i = 0; i < flashes; i++)
        {
            renderer.material.color = Color.red;
            yield return new WaitForSeconds(flashDuration);
            renderer.material.color = originalColor;
            yield return new WaitForSeconds(flashDuration);
        }
    }

    // ========================================================================
    // HELPER METHODS
    // ========================================================================

    /// <summary>
    /// Checks if the player is hiding in grass (reduces detection chance).
    /// </summary>
    public virtual bool IsPlayerInGrass()
    {
        if (PlayerTransform == null) return false;
        RaycastHit hit;
        if (Physics.Raycast(PlayerTransform.position, Vector3.down, out hit, 2f))
        {
            if (hit.collider.CompareTag("Grass")) return true;
        }
        return false;
    }

    protected virtual float GetDistanceToPlayer()
    {
        if (player == null) return Mathf.Infinity;
        return Vector3.Distance(transform.position, player.transform.position);
    }

    // ========================================================================
    // ABSTRACT METHODS (Must be implemented by derived classes)
    // ========================================================================

    public abstract void Init(Guard guard);

    public virtual void Init(Guard guard, Player _player)
    {
        this.guard = guard;
        player = _player;
        Init(guard);
    }

    /// <summary>
    /// Main update loop for AI logic - called every frame.
    /// </summary>
    public abstract void Think();

    /// <summary>
    /// Default wander/patrol behavior.
    /// </summary>
    public abstract void Wander();

    /// <summary>
    /// Chase behavior when player is detected.
    /// </summary>
    public abstract void Chase(Transform target);

    /// <summary>
    /// Moves the AI to a specific location using NavMeshAgent.
    /// </summary>
    public virtual void Seek(Vector3 location)
    {
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.SetDestination(location);
        }
    }
}