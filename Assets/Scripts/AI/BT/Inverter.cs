using UnityEngine;

// this is a inverter node just to flip a behavior tree logic , handy for specific nodes
public class Inverter : Node
{
    public Inverter(string n)
    {
        name = n;
    }


    public override Status Process()
    {
        Status childStatus = children[0].Process();

        if (childStatus == Status.RUNNING)
        {
            return Status.RUNNING;
        }

        if (childStatus == Status.FAILURE)
        {
            return Status.SUCCESS;
        }
        else
        {
            return Status.FAILURE;
        }

    }


}
