using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class Zombie : AIBrain
{
    [Header("Zombie Settings (BASELINE - SUPER DUMB)")]
    [SerializeField] private float shamblingSpeed = 1.2f;
    [SerializeField] private float detectionAngle = 60f;
    [SerializeField] private float wanderRadius = 10f;
    [SerializeField] private float waitTime = 2.5f;
    [SerializeField] private float forgetTime = 8f;

    [Header("Zombie Attack")]
    [SerializeField] private float killRange = 1.5f;  // Will override guard's kill range

    [Header("Audio")]
    [SerializeField] private AudioClip[] idleGroans;
    [SerializeField] private AudioClip[] chaseGroans;
    [SerializeField] private AudioClip attackSound;
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
    private bool hasKilledPlayer = false;

    public bool IsChasing()
    {
        return isChasing;
    }

    public override void Init(Guard guard)
    {
        this.guard = guard;

        // Ensure we have the agent (from AIBrain base class)
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
                agent = gameObject.AddComponent<NavMeshAgent>();
        }

        agent.speed = shamblingSpeed;
        agent.stoppingDistance = 0.5f;

        //
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

        Debug.Log("Zombie brain attached to guard");
    }

 
    private void WarpToNavMesh()
    {
        if (agent == null) return;

        // Try to find a valid position on NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            Debug.Log($"Zombie warped to NavMesh at {hit.position}");
        }
        else
        {
            Debug.LogError("Zombie could not find NavMesh position!");
        }
    }

    public override void Think()
    {
        if (!hasInitialised || agent == null || guard == null) return;

        // Make zombie sounds
        HandleZombieSounds();

        // Check player (via guard)
        Transform player = guard.PlayerTransform;
        if (player == null)
        {
            Wander();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // KILL PLAYER IF CLOSE ENOUGH (using guard's kill mechanism)
        if (distanceToPlayer <= killRange && !hasKilledPlayer)
        {
            KillPlayer();
            return;
        }

        // Check if player is in vision cone
        bool playerInCone = IsPlayerInCone(player);

        // CHASE if player is within detection range AND in cone
        if (distanceToPlayer <= guard.GetDetectionRange() && playerInCone)
        {
            if (!isChasing)
            {
                isChasing = true;
                chaseTimer = 0f;
                Debug.LogWarning($"Zombie SEES player at distance {distanceToPlayer:F1}! CHASING!");
            }
            Chase(player);
            return;
        }

        // If chasing but lost player
        if (isChasing)
        {
            chaseTimer += Time.deltaTime;
            if (chaseTimer >= forgetTime)
            {
                isChasing = false;
                Debug.Log("Zombie lost player - going back to shambling");
            }
            else
            {
                Seek(lastKnownPlayerPosition);
                return;
            }
        }

        // Default: dumb wandering
        Wander();
    }

    private bool IsPlayerInCone(Transform player)
    {
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        return angle <= detectionAngle;
    }

    private void KillPlayer()
    {
        if (hasKilledPlayer) return;
        hasKilledPlayer = true;

        // Play attack sound
        if (attackSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(attackSound, groanVolume * 1.2f);
        }

        Debug.Log("Zombie KILLED the player!");

        // Use guard's kill method (which handles player death)
        if (guard != null && guard.PlayerTransform != null)
        {
            Player player = guard.PlayerTransform.GetComponent<Player>();
            if (player != null)
            {
                player.Die();
            }
        }
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

    // Add to Zombie.cs
    public void Stun(float duration)
    {
        StartCoroutine(StunCoroutine(duration));
    }

    private IEnumerator StunCoroutine(float duration)
    {
        // Store original speed
        float originalSpeed = agent.speed;
        agent.speed = 0.5f; // Very slow while stunned

        yield return new WaitForSeconds(duration);

        // Restore speed
        agent.speed = originalSpeed;
    }
}