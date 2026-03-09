using System.Collections.Generic;
using UnityEngine;

// base tree for BT, i can be use as a sub tree aswell
public class BehaviourTree : Node
{
    // struct for better performance 
    struct NodeLevel
    {
        public int level;
        public Node node;
    }

    public BehaviourTree()
    {
        name = "Tree";

    }

    public override Status Process()
    {
        if(children.Count == 0)
        {
            return Status.SUCCESS;
        }

        return children[currentChild].Process();
    }

    // set name for easier to figure out
    public BehaviourTree(string n)
    {
        name = n;

    }

    // debug

    public void PrintTree()
    {
        string treePrintOut = "";

        Stack<NodeLevel> nodeStack = new Stack<NodeLevel>();

        Node currentNode = this;
        nodeStack.Push(new NodeLevel { level = 0, node = currentNode  } );

        while (nodeStack.Count != 0)
        {
            NodeLevel nextNode = nodeStack.Pop();
            treePrintOut += new string('-', nextNode.level) + nextNode.node.name + "\n";

            for (int i = nextNode.node.children.Count - 1; i >= 0; i--)
            {
                nodeStack.Push(new NodeLevel { level = nextNode.level + 1, node = nextNode.node.children[i] });

            }
        }
        Debug.Log(treePrintOut);
    }

}
