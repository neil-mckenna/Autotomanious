using System.Collections.Generic;
using UnityEngine;

public class PSelector : Node
{
    Node[] nodeArr;

    bool ordered = false;


    public PSelector(string n)
    {
        name = n;
    }

    void OrderNodes()
    {
        nodeArr = children.ToArray();

        Sort(nodeArr, 0, children.Count - 1);
        children = new List<Node>(nodeArr);

    }

    // a typical QuickSort 
    
    int Partition(Node[] array, int low, int high)
    {
        Node pivot = array[high];

        int lowIndex = (low - 1);

        //2. Reorder the collection.
        for (int j = low; j < high; j++)
        {
            if (array[j].sortOrder <= pivot.sortOrder)
            {
                lowIndex++;

                Node temp = array[lowIndex];
                array[lowIndex] = array[j];
                array[j] = temp;
            }
        }

        Node temp1 = array[lowIndex + 1];
        array[lowIndex + 1] = array[high];
        array[high] = temp1;

        return lowIndex + 1;
    }

    void Sort(Node[] array, int low, int high)
    {
        if (low < high)
        {
            int partitionIndex = Partition(array, low, high);
            Sort(array, low, partitionIndex - 1);
            Sort(array, partitionIndex + 1, high);
        }
    }

    //
    public override Node.Status Process()
    {
        // quick sort by priority nodes
        if(!ordered)
        {
            OrderNodes();
            ordered = true;
        }

        //
        Status childStatus = children[currentChild].Process();

        if (childStatus == Status.RUNNING)
        {
            
            return Status.RUNNING;
        }

        if (childStatus == Status.SUCCESS)
        {
            //children[currentChild].sortOrder = 1;
            currentChild = 0;
            ordered = false;
            return Status.SUCCESS;
        }
        //else
        //{
        //    children[currentChild].sortOrder = 10;
        //}

        currentChild++;

        if(currentChild >= children.Count)
        {
            currentChild = 0;
            ordered = false;
            return Status.FAILURE;
        }

        return Status.RUNNING;
    }

}
