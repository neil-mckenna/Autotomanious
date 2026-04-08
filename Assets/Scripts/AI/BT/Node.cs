using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// NODE - BASE CLASS FOR ALL BEHAVIOR TREE NODES
// ============================================================================
// 
// This is the foundation of the Behavior Tree system. Every node in the tree
// (Sequences, Selectors, Leaves, etc.) inherits from this class.
//
// BEHAVIOR TREE BASICS:
// - Trees are made of nodes that return SUCCESS, FAILURE, or RUNNING
// - SUCCESS: Node completed its task successfully
// - FAILURE: Node could not complete its task
// - RUNNING: Node is in progress and needs more time next frame
//
// NODE TYPES:
// - Composite Nodes: Have children (Sequence, Selector)
// - Decorator Nodes: Modify child behavior (Not, Repeat, etc.)
// - Leaf Nodes: Execute actual actions (MoveTo, Attack, etc.)
//
// ============================================================================

/// <summary>
/// Base class for all Behavior Tree nodes.
/// Provides the core functionality for node status, child management, and processing.
/// </summary>
public class Node
{
    // ========================================================================
    // PUBLIC ENUMS
    // ========================================================================

    /// <summary>
    /// Represents the execution status of a node.
    /// - SUCCESS: Node completed successfully
    /// - RUNNING: Node is still executing (needs more frames)
    /// - FAILURE: Node failed to complete
    /// </summary>
    public enum Status
    {
        SUCCESS,    // Task completed successfully
        RUNNING,    // Task is in progress (returns next frame)
        FAILURE     // Task failed to complete
    }

    // ========================================================================
    // PUBLIC PROPERTIES
    // ========================================================================

    [Tooltip("Current execution status of this node")]
    public Status status;

    [Tooltip("List of child nodes (for composite nodes like Sequence/Selector)")]
    public List<Node> children = new List<Node>();

    [Tooltip("Index of the currently executing child (used in Sequences/Selectors)")]
    public int currentChild = 0;

    [Tooltip("Human-readable name for debugging")]
    public string name;

    [Tooltip("Execution order priority (lower numbers execute first)")]
    public int sortOrder;

    // ========================================================================
    // CONSTRUCTORS
    // ========================================================================

    /// <summary>
    /// Default constructor - creates an unnamed node
    /// </summary>
    public Node()
    {
        // Empty constructor for nodes that don't need a name
    }

    /// <summary>
    /// Constructor with name for easier debugging
    /// </summary>
    /// <param name="n">Human-readable name for this node</param>
    public Node(string n)
    {
        name = n;
    }

    /// <summary>
    /// Constructor with name and sort order for priority-based execution
    /// </summary>
    /// <param name="n">Human-readable name for this node</param>
    /// <param name="order">Priority order (lower = higher priority)</param>
    public Node(string n, int order)
    {
        name = n;
        sortOrder = order;
    }

    // ========================================================================
    // VIRTUAL METHODS (OVERRIDDEN BY DERIVED CLASSES)
    // ========================================================================

    /// <summary>
    /// Processes this node and returns its execution status.
    /// This is the main method called by the Behavior Tree each frame.
    /// 
    /// VIRTUAL METHOD - Override in derived classes:
    /// - Sequence: Process children in order until one fails
    /// - Selector: Process children in order until one succeeds
    /// - Leaf: Execute actual game logic (move, attack, etc.)
    /// </summary>
    /// <returns>SUCCESS, RUNNING, or FAILURE status</returns>
    public virtual Status Process()
    {
        // Default implementation: process current child
        // This is typically overridden by composite nodes
        if (children.Count > 0 && currentChild < children.Count)
        {
            return children[currentChild].Process();
        }

        // If no children, default to SUCCESS
        return Status.SUCCESS;
    }

    // ========================================================================
    // PUBLIC METHODS
    // ========================================================================

    /// <summary>
    /// Resets this node and all its children to their initial state.
    /// Called when restarting the Behavior Tree or when a branch needs to be re-evaluated.
    /// 
    /// RESET BEHAVIOR:
    /// - Clears currentChild index (starts from first child)
    /// - Recursively resets all children
    /// - Does NOT reset status (handled by tree traversal)
    /// </summary>
    public void Reset()
    {
        // Recursively reset all child nodes
        foreach (Node n in children)
        {
            n.Reset();
        }

        // Reset to first child for next execution
        currentChild = 0;

        // Note: status is NOT reset here because it's managed by the tree traversal
        // Resetting status here could cause unexpected behavior
    }

    /// <summary>
    /// Adds a child node to this node's children list.
    /// Used when building the Behavior Tree structure.
    /// </summary>
    /// <param name="n">The child node to add</param>
    public void AddChild(Node n)
    {
        children.Add(n);
    }

    // ========================================================================
    // HELPER METHODS (FOR DEBUGGING)
    // ========================================================================

    /// <summary>
    /// Returns a formatted string representation of the node for debugging.
    /// Override in derived classes for more detailed debug info.
    /// </summary>
    public override string ToString()
    {
        string statusString = status.ToString();
        string color = GetStatusColor();
        return $"<color={color}>[{statusString}]</color> {name}";
    }

    /// <summary>
    /// Gets the color associated with each status for console debugging.
    /// </summary>
    private string GetStatusColor()
    {
        switch (status)
        {
            case Status.SUCCESS: return "green";
            case Status.RUNNING: return "yellow";
            case Status.FAILURE: return "red";
            default: return "white";
        }
    }
}

// ============================================================================
// USAGE EXAMPLE
// ============================================================================
//
// Here's how to create a simple Behavior Tree using the Node system:
//
// // Create composite nodes
// Selector root = new Selector("Root Selector");
// Sequence attackSequence = new Sequence("Attack Sequence");
//
// // Create leaf nodes (actions/conditions)
// Leaf canSeePlayer = new Leaf("Can See Player?", CheckCanSeePlayer);
// Leaf chasePlayer = new Leaf("Chase Player", ChasePlayerAction);
// Leaf attackPlayer = new Leaf("Attack Player", AttackPlayerAction);
//
// // Build the tree structure
// attackSequence.AddChild(canSeePlayer);
// attackSequence.AddChild(chasePlayer);
// attackSequence.AddChild(attackPlayer);
// root.AddChild(attackSequence);
//
// // Execute each frame
// Status result = root.Process();
//
// ============================================================================