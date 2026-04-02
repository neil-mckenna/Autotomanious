using System.Collections;
using UnityEngine;
using UnityEngine.AI;
//using static UnityEditor.Experimental.GraphView.GraphView;

public abstract class AIBrain : MonoBehaviour
{
    [Header("AIBrain Settings")]
    [SerializeField] protected NavMeshAgent agent;
    [SerializeField] protected Transform[] waypoints;

    // guard
    protected Guard Guard => guard;
    protected Guard guard;

    // player
    protected Player player;
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

    public void SetPlayer(Player newPlayer)
    {
        player = newPlayer;
        OnPlayerSet();
        Debug.Log($"{GetType().Name}: Player reference set to {(player != null ? player.name : "null")}");
    }

    
    protected virtual void OnPlayerSet() { }

    protected bool TryGetPlayer(out Player playerRef)
    {
        if (player != null)
        {
            playerRef = player;
            return true;
        }

        // Fallback to GameManager
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



    protected int currentWaypointIndex = 0;
    protected Vector3 lastKnownPlayerPosition;

    // STATUS EFFECT VARIABLES
    protected bool isStunned = false;
    protected bool isBlinded = false;
    protected bool isSlowed = false;
    protected float originalSpeed;
    protected float originalDetectionRange;
    protected Coroutine stunCoroutine;
    protected Coroutine blindCoroutine;
    protected Coroutine slowCoroutine;

    // SUSPICIOUS VARIABLES
    protected bool isSuspicious = false;
    protected Vector3 suspiciousPosition;
    protected float suspiciousTimer;
    protected float suspicionDuration = 3f;

    // KILL TRACKING
    protected bool hasKilledPlayer = false;

    // Properties
    public NavMeshAgent Agent => agent;
    
    public bool IsStunned => isStunned;
    public bool IsBlinded => isBlinded;
    public bool IsSlowed => isSlowed;
    public bool IsSuspicious => isSuspicious;

    

    protected Transform PlayerTransform => player != null ? player.transform : null;


    public void SetAgent(NavMeshAgent navAgent)
    {
        agent = navAgent;
        if (agent != null)
            originalSpeed = agent.speed;
    }

    // Player property with auto-find

    // Thread-safe player finder
    protected virtual void FindPlayer()
    {
        if (player != null) return;

        player = FindAnyObjectByType<Player>();
        if (player != null)
        {
            Debug.Log($"{GetType().Name}: Auto-found player at {player.transform.position}");
        }
        else
        {
            Invoke(nameof(FindPlayer), 0.5f);
        }
    }


    // Safe method to check if player exists
    public bool HasPlayer()
    {
        return Player != null;
    }

    //public virtual void SetPlayer(Player _player)
    //{
    //    player = _player;
    //    Debug.LogError($"{GetType().Name} player reference set to: {_player?.name}");
    //}

    #region Waypoints
    public void SetWaypoints(Transform[] newWaypoints)
    {
        waypoints = newWaypoints;
    }

    public Transform GetNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return null;
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        return waypoints[currentWaypointIndex];
    }
    #endregion

    #region Suspicion Settings

    // Simple suspicion settings
    [SerializeField] private float suspicionTime = 3f;  // Default 3 seconds

    public void SetSuspicionTime(float seconds)
    {
        suspicionTime = Mathf.Clamp(seconds, 1f, 20f);
        Debug.Log($"{GetType().Name}: Suspicion time set to {suspicionTime} seconds");
    }

    public float GetSuspicionTime()
    {
        return suspicionTime;
    }

    #endregion

    #region Vision Detection
    public virtual bool HasLineOfSightToPlayer()
    {
        if (player == null || guard == null) return false;

        // Use the guard's raycast start location for consistency
        Vector3 eyePosition = guard.RayCastStartLocation != null
            ? guard.RayCastStartLocation.position
            : transform.position + Vector3.up * 0.5f;

        Vector3 playerPosition = player.transform.position + Vector3.up * 1.5f;

        float distance = Vector3.Distance(eyePosition, playerPosition);

        if (distance > guard.GetDetectionRange()) return false;

        Vector3 directionToPlayer = (playerPosition - eyePosition).normalized;
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        float halfFOV = guard.GetFieldOfView() / 2f;

        if (angle > halfFOV) return false;

        // Line of sight check
        RaycastHit hit;
        if (Physics.Raycast(eyePosition, directionToPlayer, out hit, distance))
        {
            return hit.transform == player.transform;
        }

        return false;
    }

    #endregion

    #region Hearing Detection
    public virtual void HearNoise(Vector3 noisePosition, float noiseRadius)
    {
        Debug.Log($" {GetType().Name}.HearNoise called at {noisePosition} with radius {noiseRadius}");

        if (isStunned)
        {
            Debug.Log(" Stunned - can't hear");
            return;
        }

        float distanceToNoise = Vector3.Distance(transform.position, noisePosition);
        Debug.Log($"Distance to noise: {distanceToNoise:F1}");

        if (distanceToNoise > guard.GetMaxHearingDistance())
        {
            Debug.Log($" Too far! {distanceToNoise:F1} > {guard.GetMaxHearingDistance()}");
            return;
        }

        float effectiveRadius = Mathf.Min(noiseRadius, guard.GetMaxHearingDistance());
        Debug.Log($"Effective radius: {effectiveRadius:F1}");

        if (distanceToNoise <= effectiveRadius)
        {
            Debug.Log($" WITHIN HEARING RANGE!");

            if (HasLineOfSightToPlayer())
            {
                Debug.Log("But already sees player - ignoring");
                return;
            }

            Debug.Log($" Setting SUSPICIOUS for {suspicionTime} seconds");
            SetSuspicious(noisePosition, suspicionTime);
        }
        else
        {
            Debug.Log($" Outside effective radius: {distanceToNoise:F1} > {effectiveRadius:F1}");
        }
    }
    #endregion

    #region Kill Logic
    /// <summary>
    /// Checks if player is within kill range and kills them
    /// </summary>
    public virtual void CheckForKill()
    {
        if (hasKilledPlayer) return;
        if (Player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, Player.transform.position);
        if (distanceToPlayer <= guard.GetKillRange())
        {
            KillPlayer();
        }
    }

    /// <summary>
    /// Kills the player
    /// </summary>
    public virtual void KillPlayer()
    {
        if (hasKilledPlayer) return;
        hasKilledPlayer = true;

        Debug.Log($"{GetType().Name} killed the player!");

        if (Player != null)
        {

            Player.Die(gameObject.name);
            
        }
    }
    #endregion

    #region Suspicious Behavior
    public virtual void SetSuspicious(Vector3 position, float duration)
    {
        isSuspicious = true;
        suspiciousPosition = position;
        suspiciousTimer = duration;
        suspicionDuration = duration;
        lastKnownPlayerPosition = position;
        Debug.Log($"{GetType().Name} suspicious of noise at {position}");
    }

    protected virtual void UpdateSuspicious()
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
    #endregion

    #region Status Effects
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
    #endregion

    #region Visual Feedback
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
    #endregion

    // Helper methods
    public virtual bool IsPlayerInGrass()
    {
        if (Player.transform == null) return false;
        RaycastHit hit;
        if (Physics.Raycast(Player.transform.position, Vector3.down, out hit, 2f))
        {
            if (hit.collider.CompareTag("Grass")) return true;
        }
        return false;
    }

    // Abstract methods for child classes
    public abstract void Init(Guard guard);

    public virtual void Init(Guard guard, Player _player)
    {
        this.guard = guard;
        player = _player;
        //Debug.Log($"{GetType().Name}: Init with player {_player?.name}");

        // Call the original Init
        Init(guard);
    }

    public abstract void Think();
    public abstract void Wander();
    public abstract void Chase(Transform target);

    public virtual void Seek(Vector3 location)
    {
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.SetDestination(location);
        }
    }
}