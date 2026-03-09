using UnityEngine;
using UnityEngine.AI;

// my super abstarct class, TODO refactor more shared methods 
public abstract class  AIBrain : MonoBehaviour
{
    [SerializeField] protected NavMeshAgent agent;

    protected Guard guard;
    protected int currentWaypointIndex = 0;


    // properties
    public NavMeshAgent Agent => agent;
    public Guard Guard => guard;
   
    public void SetAgent(NavMeshAgent navAgent)
    {
        agent = navAgent;
    }

    //

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

}
