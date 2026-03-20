using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// this is for the first scene to handle the selection to pass on teh selection for the ai brains
public class MainMenu : MonoBehaviour
{
    [Header("UI Buttons")]
    [SerializeField] private Button zombieButton;
    [SerializeField] private Button fsmButton;
    [SerializeField] private Button btButton;
    [SerializeField] private Button quitButton;


    private void Start()
    {
        // Setup button listeners, handlers
        zombieButton.onClick.AddListener(() => StartGameWithAI(AISettings.AIType.Zombie));
        fsmButton.onClick.AddListener(() => StartGameWithAI(AISettings.AIType.FSM));
        btButton.onClick.AddListener(() => StartGameWithAI(AISettings.AIType.BehaviourTree));

        quitButton.onClick.AddListener(QuitGame);

    }

    // method to handle the selection, it is string sensitive so the name of matters 
    private void StartGameWithAI(AISettings.AIType aiType)
    {
        // Save the selected AI
        AISettings.Instance.selectedAIType = aiType;

        // Get the scene name from the AI settings
        string sceneToLoad = AISettings.Instance.GetSceneNameForAI(aiType);

        // Load the game scene immediately
        Debug.Log($"Starting game with {aiType} AI, loading scene: {sceneToLoad}");
        SceneManager.LoadScene(sceneToLoad);
    }

    // a simple quit editor and exe
    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
