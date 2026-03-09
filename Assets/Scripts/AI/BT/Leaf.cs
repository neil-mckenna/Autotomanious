using UnityEngine;

// leaf main end point node with muliple constructor overloads
public class Leaf : Node
{
    public delegate Status Tick();

    public Tick ProcessMethod;


    public delegate Status TickMulti(int index);

    public TickMulti ProcessMethodMulti;

    public int index;


    public Leaf()
    {
        
    }

    public Leaf(string n, Tick pm)
    {
        name = n; 
        ProcessMethod = pm;

    }

    public Leaf(string n,int i,TickMulti pm)
    {
        name = n;
        ProcessMethodMulti = pm;
        index = i;

    }

    public Leaf(string n, Tick pm, int order)
    {
        name = n;
        ProcessMethod = pm;
        sortOrder = order;
    }

    public override Status Process()
    {
        Node.Status s;

        if(ProcessMethod != null)
        {
            s = ProcessMethod();

        }
        else if (ProcessMethodMulti != null)
        {
            s = ProcessMethodMulti(index);
        }
        else
        {
            s = Status.FAILURE;
        }
         
        //Debug.Log(name + " " + s);

        return s;
    }



    
}
