using UnityEngine;
using UnityEngine.AI;
using System.Collections;

// ============================================================================
// ZOMBIE - SPECIALIZED AI BRAIN FOR ZOMBIE ENEMIES
// ============================================================================
// 
// This class implements a zombie-specific AI behavior with unique features:
// 1. Two-speed movement (slow shambling, fast running when chasing)
// 2. Cone-based vision detection (wider field of view than normal guards)
// 3. Random wandering with look-around animations
// 4. Groaning sound effects (idle and chase variants)
// 5. Special attack sounds (bite sound effect)
// 6. Memory system (forgets player after time)
//
// BEHAVIOR PRIORITY:
// 1. Kill player if in range
// 2. Chase if player is visible
// 3. Investigate if suspicious (heard noise)
// 4. Wander randomly when idle
//
// ============================================================================

public class Zombie : AIBrain
{
    // ========================================================================
    // SERIALIZED FIELDS - ZOMBIE MOVEMENT
    // ========================================================================

    [Header("=== ZOMBIE MOVEMENT SETTINGS ===")]
    [Tooltip("Movement speed when not chasing (slow, shambling walk)")]
    [SerializeField] private float shamblingSpeed = 1.2f;

    [Tooltip("Movement speed when chasing player (fast, running)")]
    [SerializeField] private float runningSpeed = 5f;

    [Tooltip("Angle of vision cone (zombies have wider vision than normal guards)")]
    [SerializeField] private float detectionAngle = 60f;

    [Tooltip("Radius for random wander destination selection")]
    [SerializeField] private float wanderRadius = 10f;

    [Tooltip("How long to pause and look around at each wander point")]
    [SerializeField] private float waitTime = 2.5f;

    [Tooltip("How long to remember player after losing sight (seconds)")]
    [SerializeField] private float forgetTime = 8f;


    // ========================================================================
    // SERIALIZED FIELDS - ZOMBIE AUDIO
    // ========================================================================

    [Header("=== ZOMBIE AUDIO CLIPS ===")]
    [Tooltip("Random groaning sounds when idle/patrolling")]
    [SerializeField] private AudioClip[] idleGroans;

    [Tooltip("Random groaning sounds when chasing (more aggressive)")]
    [SerializeField] private AudioClip[] chaseGroans;

    [Tooltip("Attack sound when killing player")]
    [SerializeField] private AudioClip attackSound;

    [Tooltip("Biting sound effect for zombie kill")]
    [SerializeField] private AudioClip biteSound;

    [Header("=== ZOMBIE AUDIO SETTINGS ===")]
    [Tooltip("Time between groans (seconds)")]
    [SerializeField] private float groanInterval = 3f;

    [Tooltip("Volume of groaning sounds (0-1)")]
    [SerializeField] private float groanVolume = 0.7f;

    [Tooltip("Multiplier for chase groan interval (lower = more frequent)")]
    [SerializeField] private float chaseGroanMultiplier = 0.5f;

    // ========================================================================
    // PRIVATE FIELDS - STATE TRACKING
    // ========================================================================

    private bool isChasing = false;       // Is zombie currently chasing player?
    private bool isWaiting = false;       // Is zombie waiting at wander point?
    private float waitTimer;              // Time remaining in wait state
    private float groanTimer;             // Time until next groan sound
    private float chaseTimer;             // Time spent chasing without seeing player
    private AudioSource audioSource;      // Reference to audio source component
    private bool hasInitialised = false;  // Has zombie been initialized?
    private float originalShambleSpeed;   // Store original shambling speed for restoration

    // ========================================================================
    // PUBLIC METHODS
    // ========================================================================

    /// <summary>
    /// Returns whether the zombie is currently chasing the player.
    /// Used for external debugging and visual effects.
    /// </summary>
    public bool IsChasing()
    {
        return isChasing;
    }

    // ========================================================================
    // PROTECTED OVERRIDES - AIBrain
    // ========================================================================

    protected override void OnPlayerSet()
    {
        Debug.Log($"Zombie: Player set - BRAINS!");
    }

    /// <summary>
    /// Initializes the zombie AI with guard reference.
    /// Sets up NavMeshAgent, audio, and initial state.
    /// </summary>
    public override void Init(Guard guard)
    {
        this.guard = guard;

        // Ensure we have the NavMeshAgent component
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
                agent = gameObject.AddComponent<NavMeshAgent>();
        }

        // Store and configure movement speeds
        originalShambleSpeed = shamblingSpeed;
        originalSpeed = shamblingSpeed;
        agent.speed = shamblingSpeed;
        agent.stoppingDistance = 0.5f;

        // Ensure zombie is properly positioned on NavMesh
        WarpToNavMesh();

        // Setup audio system for zombie sounds
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // Fully 3D positional audio
        }
        audioSource.volume = groanVolume;

        // Initialize timers
        groanTimer = Random.Range(1f, groanInterval);
        hasInitialised = true;
    }

    // ========================================================================
    // PRIVATE METHODS - INITIALIZATION HELPERS
    // ========================================================================

    /// <summary>
    /// Warps the zombie to the nearest point on the NavMesh.
    /// Prevents zombies from spawning inside walls or off the mesh.
    /// </summary>
    private void WarpToNavMesh()
    {
        if (agent == null) return;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
        else
        {
            Debug.LogError("Zombie could not find NavMesh position!");
        }
    }

    // ========================================================================
    // PUBLIC OVERRIDES - CORE AI LOGIC
    // ========================================================================

    /// <summary>
    /// Main Think loop - called every frame.
    /// Handles vision, hearing, chasing, and wandering states.
    /// 
    /// PRIORITY ORDER (highest to lowest):
    /// 1. Kill check (if player in range)
    /// 2. Chase if player visible
    /// 3. Investigate suspicious noises
    /// 4. Search if lost player recently
    /// 5. Wander aimlessly
    /// </summary>
    public override void Think()
    {
        // Safety checks
        if (!hasInitialised || agent == null || guard == null) return;
        if (isStunned) return;

        // Update audio (groaning)
        HandleZombieSounds();

        // Priority 1: Check for kill opportunity
        CheckForKill();

        // Get player reference
        if (!TryGetPlayer(out Player currentPlayer))
        {
            Wander();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, Player.transform.position);
        bool hasLineOfSight = HasLineOfSightToPlayer();
        bool playerInCone = IsPlayerInCone(Player.transform);
        float currentDetectionRange = guard.GetDetectionRange();

        // Priority 2: CHASE if player is visible
        if (hasLineOfSight && playerInCone && distanceToPlayer <= currentDetectionRange)
        {
            if (!isChasing)
            {
                isChasing = true;
                chaseTimer = 0f;
                agent.speed = runningSpeed;
                //Debug.Log($"ZOMBIE SEES YOU! Running at {runningSpeed}!");
            }
            Chase(Player.transform);
            return;
        }

        // Priority 3: Investigate suspicious noises (hearing)
        if (isSuspicious && suspiciousTimer > 0)
        {
            suspiciousTimer -= Time.deltaTime;
            if (!isChasing)
            {
                isChasing = true;
                chaseTimer = 0f;
                agent.speed = runningSpeed;
                //Debug.Log($"ZOMBIE HEARD NOISE! Investigating at {suspiciousPosition}!");
            }
            Seek(suspiciousPosition);
            return;
        }

        // Priority 4: Search for lost player (memory)
        if (isChasing)
        {
            chaseTimer += Time.deltaTime;
            if (chaseTimer >= forgetTime)
            {
                // Player forgotten - return to shambling
                isChasing = false;
                agent.speed = shamblingSpeed;
                ResetSuspicious();
                //Debug.Log("Zombie lost player - going back to shambling");
            }
            else
            {
                // Move to last known player position
                if (lastKnownPlayerPosition != Vector3.zero)
                    Seek(lastKnownPlayerPosition);
                return;
            }
        }

        // Priority 5: Default wander behavior
        Wander();
    }

    // ========================================================================
    // PRIVATE METHODS - VISION
    // ========================================================================

    /// <summary>
    /// Checks if player is within the zombie's vision cone.
    /// Zombies have a fixed detection angle (wider than normal guards).
    /// </summary>
    private bool IsPlayerInCone(Transform player)
    {
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        return angle <= detectionAngle;
    }

    // ========================================================================
    // PUBLIC OVERRIDES - COMBAT
    // ========================================================================

    /// <summary>
    /// Override kill method to add zombie-specific sound effects.
    /// Plays attack sound immediately, then bite sound after short delay.
    /// </summary>
    public override void KillPlayer(string killRange, Player playerToKill = null)
    {
        if (hasKilledPlayer) return;
        hasKilledPlayer = true;

        // Play attack sound (immediate)
        if (attackSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(attackSound, groanVolume * 1.2f);
        }

        // Play bite sound (delayed for dramatic effect)
        if (biteSound != null && audioSource != null)
        {
            StartCoroutine(PlayBiteSound());
        }

        Debug.LogWarning($"{gameObject.name} BIT and KILLED the player at {killRange} meters distance!");

        // Kill the player
        if (Player != null)
        {
            Player.Die(gameObject.name, gameObject);
        }
    }

    /// <summary>
    /// Coroutine to play bite sound after a short delay.
    /// Creates a more dramatic kill effect.
    /// </summary>
    private IEnumerator PlayBiteSound()
    {
        yield return new WaitForSeconds(0.1f);
        audioSource.PlayOneShot(biteSound, groanVolume * 1.5f);
    }

    // ========================================================================
    // PRIVATE METHODS - AUDIO
    // ========================================================================

    /// <summary>
    /// Handles zombie groaning sounds.
    /// Different sounds for idle vs chasing states.
    /// Chase groans play more frequently (multiplier applied).
    /// </summary>
    private void HandleZombieSounds()
    {
        if (audioSource == null) return;

        // Calculate interval based on state (chasing = more frequent)
        float currentInterval = isChasing ? groanInterval * chaseGroanMultiplier : groanInterval;

        groanTimer -= Time.deltaTime;
        if (groanTimer <= 0)
        {
            AudioClip clip = null;

            // Select appropriate clip based on state
            if (isChasing && chaseGroans.Length > 0)
            {
                clip = chaseGroans[Random.Range(0, chaseGroans.Length)];
            }
            else if (!isChasing && idleGroans.Length > 0)
            {
                clip = idleGroans[Random.Range(0, idleGroans.Length)];
            }

            // Play the selected clip
            if (clip != null)
            {
                audioSource.PlayOneShot(clip, groanVolume);
                groanTimer = Random.Range(currentInterval * 0.5f, currentInterval * 1.5f);
            }
        }
    }

    // ========================================================================
    // PUBLIC OVERRIDES - MOVEMENT BEHAVIORS
    // ========================================================================

    /// <summary>
    /// Wander behavior for zombies.
    /// Moves to random destinations within wander radius.
    /// Includes wait states with head-turning animations.
    /// </summary>
    public override void Wander()
    {
        if (agent == null) return;

        // Handle waiting state
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0)
            {
                isWaiting = false;
                PickRandomDestination();
            }
            return;
        }

        // Reached destination - start waiting and looking around
        if (!agent.hasPath || agent.remainingDistance < 0.5f)
        {
            isWaiting = true;
            waitTimer = waitTime;
            agent.ResetPath();
            StartCoroutine(ShambleLookAround());
        }
    }

    /// <summary>
    /// Chase behavior - moves directly toward player.
    /// Updates last known position for search behavior.
    /// </summary>
    public override void Chase(Transform target)
    {
        if (target == null || agent == null) return;
        agent.SetDestination(target.position);
        lastKnownPlayerPosition = target.position;
    }

    // ========================================================================
    // PRIVATE METHODS - WANDER HELPERS
    // ========================================================================

    /// <summary>
    /// Coroutine that makes the zombie look around during wait state.
    /// Creates more natural, creepy zombie behavior.
    /// </summary>
    private IEnumerator ShambleLookAround()
    {
        float lookTime = 0f;
        Quaternion startRot = transform.rotation;
        Quaternion targetRot = startRot * Quaternion.Euler(0, Random.Range(-90f, 90f), 0);

        while (lookTime < 1f)
        {
            lookTime += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(startRot, targetRot, lookTime);
            yield return null;
        }
    }

    /// <summary>
    /// Selects a random destination within wander radius.
    /// Ensures destination is on NavMesh before setting.
    /// </summary>
    private void PickRandomDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    // ========================================================================
    // PROTECTED OVERRIDES - STATUS EFFECTS
    // ========================================================================

    /// <summary>
    /// Override stun to restore zombie-specific speed after stun ends.
    /// Restores either running or shambling speed based on chase state.
    /// </summary>
    protected override IEnumerator StunCoroutine(float duration)
    {
        isStunned = true;

        float storedSpeed = agent.speed;
        agent.speed = 0f;
        agent.isStopped = true;
        agent.ResetPath();

        ApplyStunVisual();
        Debug.Log($"Zombie STUNNED for {duration} seconds!");

        yield return new WaitForSeconds(duration);

        // Restore speed based on whether chasing or not
        agent.speed = isChasing ? runningSpeed : shamblingSpeed;
        agent.isStopped = false;
        RemoveStunVisual();

        isStunned = false;
        Debug.Log($"Zombie recovered from stun");
    }

    // ========================================================================
    // PROTECTED OVERRIDES - VISUAL FEEDBACK
    // ========================================================================
    // Zombie-specific visual effects for status changes.
    // Colors are applied to the zombie's material for feedback.

    protected override void ApplyStunVisual()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = Color.yellow;
    }

    protected override void RemoveStunVisual()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = Color.white;
    }

    protected override void ApplyBlindVisual()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = Color.gray;
    }

    protected override void RemoveBlindVisual()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = Color.white;
    }

    protected override void ApplySlowVisual()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = Color.cyan;
    }

    protected override void RemoveSlowVisual()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = Color.white;
    }
}