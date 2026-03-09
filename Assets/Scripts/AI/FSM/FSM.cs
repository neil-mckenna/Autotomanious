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
        Chasing,
        Searching
    }

    #endregion

    #region Properties

    private GuardState currentState = GuardState.Patrolling;
    private float searchTimer;
    private float searchDuration = 3f;

    private Vector3 lastKnownPlayerPosition;
    private bool hasInitialised = false;

    private bool wasPlayerVisible = false;

    [SerializeField] public Transform[] waypoints;

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
        //Debug.Log($"--- Patrolling Update --- Can see player: {canSeePlayer}");

        if (canSeePlayer)
        {
            currentState = GuardState.Chasing;
            lastKnownPlayerPosition = guard.PlayerTransform.position;

            // First time seeing player in this encounter
            if (!wasPlayerVisible)
            {
                wasPlayerVisible = true;
                if (GameManager.Instance != null)
                    GameManager.Instance.GuardAlerted();
            }

            // Always count as spotted
            if (GameManager.Instance != null)
                GameManager.Instance.PlayerSpotted();

            return;
        }
        else
        {
            // Player lost while patrolling
            if (wasPlayerVisible)
            {
                wasPlayerVisible = false;
                // update the timer
                if (GameManager.Instance != null)
                    GameManager.Instance.GuardLostPlayer(); // Stop timer
            }
        }

        Wander();
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

        // keep counting spotted frames while chasing
        if (guard.HasLineOfSightToPlayer())
        {
            // update the game manager
            if (GameManager.Instance != null)
                GameManager.Instance.PlayerSpotted();
        }

        // enter searching state
        if (!guard.HasLineOfSightToPlayer())
        {
            currentState = GuardState.Searching;
            searchTimer = searchDuration;
            lastKnownPlayerPosition = guard.PlayerTransform.position;

            // player lost while chasing
            if (wasPlayerVisible)
            {
                wasPlayerVisible = false;
                if (GameManager.Instance != null)
                    GameManager.Instance.GuardLostPlayer(); // Stop timer
            }
        }

    }

    #endregion

    #region Searching

    private void UpdateSearching()
    {
        searchTimer -= Time.deltaTime;

        if (agent != null && !agent.pathPending && agent.remainingDistance < 0.5f)
        {
            Vector3 searchPosition = lastKnownPlayerPosition + Random.insideUnitSphere * 5f;
            searchPosition.y = lastKnownPlayerPosition.y;
            Seek(searchPosition);

            //Debug.Log($"Searching at {searchPosition}");
        }

        if (guard.HasLineOfSightToPlayer())
        {
            currentState = GuardState.Chasing;
            //Debug.Log("Found Player - switching back to CHASE STATE");
            return;
        }

        if (searchTimer <= 0)
        {
            currentState = GuardState.Patrolling;
            //Debug.Log("Search ended - back to Patrol STATE");
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

    #region Waypoints

    public void SetWaypoints(Transform[] newWaypoints)
    {
        waypoints = newWaypoints;
    }

    protected Transform GetNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            return null;
        }

        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        return waypoints[currentWaypointIndex];
    }

    #endregion

}

