using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class Zombie : AIBrain
{
    [Header("Zombie Settings")]
    [SerializeField] private float shamblingSpeed = 1.2f;
    [SerializeField] private float runningSpeed = 5f;
    [SerializeField] private float detectionAngle = 60f;
    [SerializeField] private float wanderRadius = 10f;
    [SerializeField] private float waitTime = 2.5f;
    [SerializeField] private float forgetTime = 8f;

    [Header("Zombie Attack")]
    [SerializeField] private float killRange = 1.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip[] idleGroans;
    [SerializeField] private AudioClip[] chaseGroans;
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioClip biteSound;
    [SerializeField] private float groanInterval = 3f;
    [SerializeField] private float groanVolume = 0.7f;
    [SerializeField] private float chaseGroanMultiplier = 0.5f;

    // Zombie state
    private bool isChasing = false;
    private bool isWaiting = false;
    private float waitTimer;
    private float groanTimer;
    private float chaseTimer;
    private AudioSource audioSource;
    private bool hasInitialised = false;
    private float originalShambleSpeed;

    public bool IsChasing()
    {
        return isChasing;
    }

    protected override void OnPlayerSet()
    {
        Debug.Log($"Zombie: Player set - BRAINS!");
    }

    public override void Init(Guard guard)
    {
        this.guard = guard;

        // Ensure we have the agent
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
                agent = gameObject.AddComponent<NavMeshAgent>();
        }

        // Store speeds
        originalShambleSpeed = shamblingSpeed;
        originalSpeed = shamblingSpeed;
        agent.speed = shamblingSpeed;
        agent.stoppingDistance = 0.5f;

        // Warp to NavMesh
        WarpToNavMesh();

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
        }
        audioSource.volume = groanVolume;

        groanTimer = Random.Range(1f, groanInterval);
        hasInitialised = true;

        Debug.Log("Zombie initialized - slow shambler, fast runner!");
    }

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

    public override void Think()
    {
        if (!hasInitialised || agent == null || guard == null) return;
        if (isStunned) return;

        // Make zombie sounds
        HandleZombieSounds();

        // Check for kill (uses base class method)
        CheckForKill();

        // Check player (using base class playerTransform)
        if (!TryGetPlayer(out Player currentPlayer))
        {
            Wander();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, Player.transform.position);

        // Use base class vision detection
        bool hasLineOfSight = HasLineOfSightToPlayer();
        bool playerInCone = IsPlayerInCone(Player.transform);
        float currentDetectionRange = guard.GetDetectionRange();

        // CHASE if player is seen
        if (hasLineOfSight && playerInCone && distanceToPlayer <= currentDetectionRange)
        {
            if (!isChasing)
            {
                isChasing = true;
                chaseTimer = 0f;
                agent.speed = runningSpeed;
                Debug.Log($"ZOMBIE SEES YOU! Running at {runningSpeed}!");
            }
            Chase(Player.transform);
            return;
        }

        // Sound detection (uses base class suspicious)
        if (isSuspicious && suspiciousTimer > 0)
        {
            suspiciousTimer -= Time.deltaTime;
            if (!isChasing)
            {
                isChasing = true;
                chaseTimer = 0f;
                agent.speed = runningSpeed;
                Debug.Log($"ZOMBIE HEARD NOISE! Investigating at {suspiciousPosition}!");
            }
            Seek(suspiciousPosition);
            return;
        }

        // Lost player
        if (isChasing)
        {
            chaseTimer += Time.deltaTime;
            if (chaseTimer >= forgetTime)
            {
                isChasing = false;
                agent.speed = shamblingSpeed;
                ResetSuspicious();  // Reset using base method
                Debug.Log("Zombie lost player - going back to shambling");
            }
            else
            {
                if (lastKnownPlayerPosition != Vector3.zero)
                    Seek(lastKnownPlayerPosition);
                return;
            }
        }

        // Default: wander
        Wander();
    }

    private bool IsPlayerInCone(Transform player)
    {
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        return angle <= detectionAngle;
    }

    // Override kill for zombie-specific sounds
    public override void KillPlayer()
    {
        if (hasKilledPlayer) return;
        hasKilledPlayer = true;

        // Play attack sound
        if (attackSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(attackSound, groanVolume * 1.2f);
        }

        // Play bite sound for zombie
        if (biteSound != null && audioSource != null)
        {
            StartCoroutine(PlayBiteSound());
        }

        Debug.LogWarning($"{gameObject.name} BIT and KILLED the player!");


        if (Player != null)
        {
            Player.Die(gameObject.name);
        }
        
    }

    private IEnumerator PlayBiteSound()
    {
        yield return new WaitForSeconds(0.1f);
        audioSource.PlayOneShot(biteSound, groanVolume * 1.5f);
    }

    private void HandleZombieSounds()
    {
        if (audioSource == null) return;

        float currentInterval = isChasing ? groanInterval * chaseGroanMultiplier : groanInterval;

        groanTimer -= Time.deltaTime;
        if (groanTimer <= 0)
        {
            AudioClip clip = null;

            if (isChasing && chaseGroans.Length > 0)
            {
                clip = chaseGroans[Random.Range(0, chaseGroans.Length)];
            }
            else if (!isChasing && idleGroans.Length > 0)
            {
                clip = idleGroans[Random.Range(0, idleGroans.Length)];
            }

            if (clip != null)
            {
                audioSource.PlayOneShot(clip, groanVolume);
                groanTimer = Random.Range(currentInterval * 0.5f, currentInterval * 1.5f);
            }
        }
    }

    public override void Wander()
    {
        if (agent == null) return;

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

        if (!agent.hasPath || agent.remainingDistance < 0.5f)
        {
            isWaiting = true;
            waitTimer = waitTime;
            agent.ResetPath();
            StartCoroutine(ShambleLookAround());
        }
    }

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

    private void PickRandomDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    public override void Chase(Transform target)
    {
        if (target == null || agent == null) return;
        agent.SetDestination(target.position);
        lastKnownPlayerPosition = target.position;
    }

    // Override stun to restore zombie-specific speed
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

    // Override visual feedback for zombie colors
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