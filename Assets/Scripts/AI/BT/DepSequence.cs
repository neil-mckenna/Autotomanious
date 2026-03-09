using UnityEngine;
using UnityEngine.AI;

// this is a sequnce node than handle and relies on the dependy success
public class DepSequence : Node
{
    BehaviourTree dependency;
    NavMeshAgent agent;

    public DepSequence(string n, BehaviourTree d, NavMeshAgent a)
    {
        name = n;
        dependency = d;
        agent = a;
    }

    public override Node.Status Process()
    {
        if(dependency.Process() == Status.FAILURE)
        {
            agent.ResetPath();
            // reset kids
            foreach(Node n in children)
            {
                n.Reset();
            }
            return Status.FAILURE;
        }


        Status childStatus = children[currentChild].Process();

        if(childStatus == Status.RUNNING)
        {
            return Status.RUNNING;
        }

        if (childStatus == Status.FAILURE)
        {
            return childStatus;
        }

        currentChild++;

        if(currentChild >= children.Count)
        {
            currentChild = 0;
            return Status.SUCCESS;
        }

        return Node.Status.RUNNING;
    }

}
