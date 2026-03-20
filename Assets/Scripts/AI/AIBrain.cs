using UnityEngine;
using UnityEngine.AI;

// my super abstarct class, TODO refactor more shared methods 
public abstract class  AIBrain : MonoBehaviour
{
    [SerializeField] protected NavMeshAgent agent;
    [SerializeField] protected Transform[] waypoints;

    protected Guard guard;
    protected int currentWaypointIndex = 0;

    protected Vector3 lastKnownPlayerPosition;

    protected Transform playerTransform;

    // properties
    public NavMeshAgent Agent => agent;
    public Guard Guard => guard;

    public Transform PlayerTransform => playerTransform;

    public void SetAgent(NavMeshAgent navAgent)
    {
        agent = navAgent;
    }
    public virtual void SetPlayer(Transform player)
    {
        playerTransform = player;
        Debug.Log($"{GetType().Name} player reference set to: {player?.name}");
    }


    #region Waypoints
    public void SetWaypoints(Transform[] newWaypoints)
    {
        waypoints = newWaypoints;
    }


    public Transform GetNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            return null;
        }

        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        return waypoints[currentWaypointIndex];
    }

    #endregion



    // methods that all AI guards will need to do , maybe add kill 
    public abstract void Init(Guard guard);
    public abstract void Think();
    public abstract void Wander();
    public abstract void Chase(Transform target);

    // move agent on navmesh to a location 
    public virtual void Seek(Vector3 location)
    {
        if (agent != null && agent.isActiveAndEnabled)
        {
            //Debug.LogWarning($"SEEKING to {location}");
            agent.SetDestination(location);
        }
        else
        {
            //Debug.LogWarning("Agent is null or inactive");
        }
    }

    public virtual bool IsPlayerInGrass()
    {
        if (playerTransform == null) return false;

        // Simple raycast down from player to check for grass
        RaycastHit hit;
        if (Physics.Raycast(playerTransform.position, Vector3.down, out hit, 2f))
        {
            if (hit.collider.CompareTag("Grass"))
            {
                return true;
            }
        }

        return false;
    }

}
