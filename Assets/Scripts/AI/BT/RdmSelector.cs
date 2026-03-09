using System.Collections.Generic;
using UnityEngine;

// a random selector for behavior tree
public class RdmSelector : Node
{
    bool shuffled = false;

    public RdmSelector(string n)
    {
        name = n;
    }
    public override Node.Status Process()
    {
        if (!shuffled)
        {
            children.Shuffle();
            shuffled = true;
        }

        Status childStatus = children[currentChild].Process();

        if (childStatus == Status.RUNNING)
        {
            
            return Status.RUNNING;
        }

        if (childStatus == Status.SUCCESS)
        {
            currentChild = 0;
            shuffled = false;
            return Status.SUCCESS;
        }

        currentChild++;

        if(currentChild >= children.Count)
        {
            currentChild = 0;
            shuffled = false;
            return Status.FAILURE;
        }

        return Status.RUNNING;

    }


}
