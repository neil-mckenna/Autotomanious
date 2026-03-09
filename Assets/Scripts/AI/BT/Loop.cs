using UnityEngine;

// a loop in the beahvioral tree
public class Loop : Node
{
    BehaviourTree dependency;

    public Loop(string n, BehaviourTree d)
    {
        name = n;
        dependency = d;
    }

    public override Node.Status Process()
    {
        if (dependency.Process() == Status.FAILURE)
        {
            return Node.Status.SUCCESS;
        }

        Status childStatus = children[currentChild].Process();

        if (childStatus == Status.RUNNING)
        {
            return Status.RUNNING;
        }

        if (childStatus == Status.FAILURE)
        {
            currentChild = 0;
            foreach (Node n in children)
            {
                n.Reset();
            }
            return childStatus;
        }

        currentChild++;

        if (currentChild >= children.Count)
        {
            currentChild = 0;
        }

        return Node.Status.RUNNING;
    }

}
