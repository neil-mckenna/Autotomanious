using UnityEngine;

// scritable object for unity Menu to create a new custom settings

[CreateAssetMenu(fileName = "AISettings", menuName = "AISettings")]
public class AISettings : ScriptableObject
{
    // enum more to add
    public enum AIType { FSM, BehaviourTree }
    public AIType selectedAIType = AIType.FSM;

    // Scene names for each AI type !!! warning string names higly sensitive to match
    [Header("Scene Assignments")]
    [SerializeField] private string fsmSceneName = "FSM_Level";
    [SerializeField] private string behaviourTreeSceneName = "BT_Level";

    // fancy singleton 
    private static AISettings instance;
    public static AISettings Instance
    {
        get 
        { 
            if(instance == null)
            {
                instance = Resources.Load<AISettings>("AISettings");

                if(instance == null)
                {
                    instance = CreateInstance<AISettings>();
#if UNITY_EDITOR
                    UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/Resources/AISettings.asset");
                    UnityEditor.AssetDatabase.SaveAssets();
#endif
                }
            }

            return instance;
        }
    }

    // Get the appropriate scene name based on AI type
    public string GetSceneNameForAI(AIType aiType)
    {
        // TODO need to add other here for 3rd option
        switch (aiType)
        {
            case AIType.FSM:
                return fsmSceneName;
            case AIType.BehaviourTree:
                return behaviourTreeSceneName;
            default:
                Debug.LogError($"Unknown AI type: {aiType}");
                return fsmSceneName; // Default fallback
        }
    }

    // for ui
    public void SelectFSM() => selectedAIType = AIType.FSM;
    public void SelectBehaviorTree() => selectedAIType = AIType.BehaviourTree;


}
