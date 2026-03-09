using UnityEngine;

// selector method for behavior tree and look for teh first true one, success
public class Selector : Node
{
    public Selector(string n)
    {
        name = n;
    }

    public override Node.Status Process()
    {
        Status childStatus = children[currentChild].Process();


        if (childStatus == Status.RUNNING)
        {
            
            return Status.RUNNING;
        }

        if (childStatus == Status.SUCCESS)
        {
            currentChild = 0;
            return Status.SUCCESS;
        }

        currentChild++;

        if(currentChild >= children.Count)
        {
            currentChild = 0;
            return Status.FAILURE;
        }

        return Status.RUNNING;

    }

}
