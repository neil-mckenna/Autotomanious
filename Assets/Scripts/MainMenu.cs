using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================================
// MAIN MENU - AI SELECTION AND SCENE MANAGEMENT
// ============================================================================
// 
// This script handles the main menu UI for selecting which AI type to play against.
// It allows players to choose between different AI implementations for scientific
// comparison and gameplay variety.
//
// AI TYPES AVAILABLE:
// 1. Zombie AI - Specialized zombie behavior with unique movement and sounds
// 2. FSM (Finite State Machine) - Traditional state-based AI (no alerts)
// 3. Behaviour Tree - Modern behavior tree AI (with alert/bark system)
//
// HOW IT WORKS:
// - Player clicks a button corresponding to an AI type
// - The selection is saved to AISettings (persistent across scenes)
// - The appropriate scene is loaded based on the AI type
// - The game scene reads AISettings to know which AI brain to spawn
//
// SCIENTIFIC PURPOSE:
// This allows controlled comparison between different AI architectures:
// - Individual AI (FSM) vs Coordinated AI (Behaviour Tree)
// - Traditional AI (FSM) vs Specialized AI (Zombie)
// - Performance metrics across different AI implementations
//
// ============================================================================

public class MainMenu : MonoBehaviour
{
    // ========================================================================
    // SERIALIZED FIELDS - UI BUTTON REFERENCES
    // ========================================================================

    [Header("=== AI SELECTION BUTTONS ===")]
    [Tooltip("Button for selecting Zombie AI")]
    [SerializeField] private Button zombieButton;

    [Tooltip("Button for selecting FSM (Finite State Machine) AI")]
    [SerializeField] private Button fsmButton;

    [Tooltip("Button for selecting Behaviour Tree AI")]
    [SerializeField] private Button btButton;

    [Header("=== UTILITY BUTTONS ===")]
    [Tooltip("Button for quitting the game")]
    [SerializeField] private Button quitButton;

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    private void Start()
    {
        // Setup button click listeners
        SetupButtonListeners();
    }

    // ========================================================================
    // INITIALIZATION METHODS
    // ========================================================================

    /// <summary>
    /// Assigns click handlers to all UI buttons.
    /// Each button triggers a different AI type selection.
    /// </summary>
    private void SetupButtonListeners()
    {
        // AI Selection Buttons
        zombieButton.onClick.AddListener(() => StartGameWithAI(AISettings.AIType.Zombie));
        fsmButton.onClick.AddListener(() => StartGameWithAI(AISettings.AIType.FSM));
        btButton.onClick.AddListener(() => StartGameWithAI(AISettings.AIType.BehaviourTree));

        // Utility Buttons
        quitButton.onClick.AddListener(QuitGame);
    }

    // ========================================================================
    // GAME START METHODS
    // ========================================================================

    /// <summary>
    /// Starts the game with the selected AI type.
    /// Saves the selection to AISettings and loads the appropriate scene.
    /// </summary>
    /// <param name="aiType">The AI type selected by the player</param>
    private void StartGameWithAI(AISettings.AIType aiType)
    {
        // Save the selected AI type to persistent settings
        AISettings.Instance.selectedAIType = aiType;

        // Get the scene name associated with this AI type
        // (Different scenes may have different level designs for each AI)
        string sceneToLoad = AISettings.Instance.GetSceneNameForAI(aiType);

        // Log the selection for debugging
        Debug.Log($"Starting game with {aiType} AI, loading scene: {sceneToLoad}");

        // Load the game scene
        SceneManager.LoadScene(sceneToLoad);
    }

    // ========================================================================
    // UTILITY METHODS
    // ========================================================================

    /// <summary>
    /// Quits the game application.
    /// Works in both Unity Editor and built executable.
    /// </summary>
    private void QuitGame()
    {
#if UNITY_EDITOR
        // Stop play mode in Unity Editor
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Quit the built application
        Application.Quit();
#endif
    }
}