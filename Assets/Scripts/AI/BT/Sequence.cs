using UnityEngine;

// a node to go through all the sequence for teh behavioral tree
public class Sequence : Node
{
    public Sequence(string n)
    {
        name = n;
    }

    public override Node.Status Process()
    {
        Status childStatus = children[currentChild].Process();

        if(childStatus == Status.RUNNING)
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

        if(currentChild >= children.Count)
        {
            currentChild = 0;
            return Status.SUCCESS;
        }

        return Node.Status.RUNNING;
    }

}
