using UnityEngine;

// ============================================================================
// AI SETTINGS - SCRIPTABLE OBJECT FOR GLOBAL AI CONFIGURATION
// ============================================================================
// 
// This ScriptableObject stores persistent AI settings that persist across scenes.
// It acts as a global configuration container for AI type selection and scene mapping.
//
// KEY FEATURES:
// 1. Singleton pattern for easy access from any script
// 2. Persistent AI type selection across scene loads
// 3. Scene name mapping for each AI type
// 4. Automatic asset creation if missing
// 5. UI helper methods for button callbacks
//
// USE CASES:
// - Main Menu: Save selected AI type before loading game scene
// - Scene Spawner: Read selected AI type to spawn correct brain prefab
// - GameManager: Display current AI type in HUD
//
// HOW TO USE:
// 1. Right-click in Project window -> Create -> AISettings
// 2. Configure scene names for each AI type
// 3. Access via AISettings.Instance from any script
//
// ============================================================================

[CreateAssetMenu(fileName = "AISettings", menuName = "AISettings")]
public class AISettings : ScriptableObject
{
    // ========================================================================
    // PUBLIC ENUMS
    // ========================================================================

    /// <summary>
    /// Available AI types for the game.
    /// Add new AI types here as they are implemented.
    /// </summary>
    public enum AIType
    {
        Zombie,         // Specialized zombie AI with unique behavior
        FSM,            // Finite State Machine (traditional, individual responses)
        BehaviourTree   // Behavior Tree (modern, with alert system)
    }

    // ========================================================================
    // PUBLIC FIELDS - AI SELECTION
    // ========================================================================

    [Tooltip("Currently selected AI type (persists across scenes)")]
    public AIType selectedAIType = AIType.FSM;

    // ========================================================================
    // SERIALIZED FIELDS - SCENE ASSIGNMENTS
    // ========================================================================

    [Header("=== SCENE ASSIGNMENTS ===")]
    [Tooltip("Name of the scene for Zombie AI (case-sensitive)")]
    [SerializeField] private string zombieTreeSceneName = "Zombie_Level";

    [Tooltip("Name of the scene for FSM AI (case-sensitive)")]
    [SerializeField] private string fsmSceneName = "FSM_Level";

    [Tooltip("Name of the scene for Behaviour Tree AI (case-sensitive)")]
    [SerializeField] private string behaviourTreeSceneName = "BT_Level";

    // ========================================================================
    // SINGLETON IMPLEMENTATION
    // ========================================================================

    private static AISettings instance;

    /// <summary>
    /// Singleton instance for global access.
    /// Automatically loads from Resources folder or creates new asset if missing.
    /// </summary>
    public static AISettings Instance
    {
        get
        {
            if (instance == null)
            {
                // Try to load existing settings from Resources folder
                instance = Resources.Load<AISettings>("AISettings");

                if (instance == null)
                {
                    // Create new instance if none exists
                    instance = CreateInstance<AISettings>();

#if UNITY_EDITOR
                    // Ensure Resources folder exists
                    if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
                    {
                        UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
                    }

                    // Save the new asset to disk
                    UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/Resources/AISettings.asset");
                    UnityEditor.AssetDatabase.SaveAssets();
                    Debug.Log("AISettings: Created new settings asset at Assets/Resources/AISettings.asset");
#endif
                }
            }

            return instance;
        }
    }

    // ========================================================================
    // PUBLIC METHODS - SCENE NAME LOOKUP
    // ========================================================================

    /// <summary>
    /// Gets the scene name associated with the specified AI type.
    /// </summary>
    /// <param name="aiType">The AI type to get scene name for</param>
    /// <returns>Scene name string (case-sensitive, must match build settings)</returns>
    public string GetSceneNameForAI(AIType aiType)
    {
        switch (aiType)
        {
            case AIType.Zombie:
                return zombieTreeSceneName;

            case AIType.FSM:
                return fsmSceneName;

            case AIType.BehaviourTree:
                return behaviourTreeSceneName;

            default:
                Debug.LogError($"AISettings: Unknown AI type: {aiType}. Defaulting to FSM.");
                return fsmSceneName;
        }
    }

    // ========================================================================
    // PUBLIC METHODS - UI HELPERS
    // ========================================================================

    /// <summary>
    /// Helper method for UI button callbacks.
    /// Selects Zombie AI type.
    /// </summary>
    public void SelectZombie()
    {
        selectedAIType = AIType.Zombie;
        Debug.Log($"AISettings: Zombie AI selected");
    }

    /// <summary>
    /// Helper method for UI button callbacks.
    /// Selects FSM AI type.
    /// </summary>
    public void SelectFSM()
    {
        selectedAIType = AIType.FSM;
        Debug.Log($"AISettings: FSM AI selected");
    }

    /// <summary>
    /// Helper method for UI button callbacks.
    /// Selects Behaviour Tree AI type.
    /// </summary>
    public void SelectBehaviorTree()
    {
        selectedAIType = AIType.BehaviourTree;
        Debug.Log($"AISettings: Behaviour Tree AI selected");
    }
}