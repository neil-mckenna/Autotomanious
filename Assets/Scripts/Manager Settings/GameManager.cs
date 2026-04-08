using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// ============================================================================
// GAMEMANAGER - CENTRAL GAME CONTROLLER AND STATS TRACKING
// ============================================================================
// 
// This is the core manager for the entire game. It handles:
// 1. Game state management (active, paused, frozen)
// 2. Player tracking and registration
// 3. UI management (HUD, victory, game over panels)
// 4. Statistics tracking (time, deaths, alerts, stealth score)
// 5. Record keeping (best times, best stealth scores)
// 6. Scene management (restart, main menu)
// 7. Pause system with time scale control
// 8. Zone-based time tracking (with ZoneManager integration)
//
// SINGLETON PATTERN:
// - Only one instance exists across scene loads (DontDestroyOnLoad)
// - Access via GameManager.Instance from any script
//
// ============================================================================

public class GameManager : MonoBehaviour
{
    // ========================================================================
    // SINGLETON
    // ========================================================================

    #region Singleton
    /// <summary>
    /// Singleton instance - persists across scene loads.
    /// Access via GameManager.Instance from any script.
    /// </summary>
    public static GameManager Instance { get; private set; }
    #endregion

    // ========================================================================
    // PRIVATE FIELDS - GAME STATE
    // ========================================================================

    #region Fields
    private Player currentPlayer;           // Reference to the player
    private Camera playerCamera;            // Reference to player's camera for UI
    private bool isGameActive = true;       // Is the game currently running?
    private bool isPaused = false;          // Is the game paused?
    private bool isGameFrozen = false;      // Is the game frozen (victory/game over)?

    // ========================================================================
    // STATISTICS TRACKING
    // ========================================================================

    private float levelStartTime;            // When the current level started
    private float currentTime;               // Current level time
    private int deathCount = 0;              // Number of deaths this run
    private int escapeCount = 0;             // Number of escapes (usually 0 or 1)
    private int alertCount = 0;              // Number of times guards were alerted
    private int timesSpotted = 0;            // Number of times player was seen
    private float stealthScore = 0f;         // Calculated stealth performance (0-1000)
    private int retries = 0;                 // Number of retry attempts
    private float totalAlertTime = 0f;       // Total time spent in alert state

    // ========================================================================
    // PERSONAL BEST RECORDS (Persistent across sessions)
    // ========================================================================

    private float fastestEscape = Mathf.Infinity;      // Best completion time
    private float bestStealthRating = 0f;               // Best stealth score
    private int fewestAlerts = int.MaxValue;            // Lowest alert count
    private int fewestDeaths = int.MaxValue;            // Lowest death count
    #endregion

    // ========================================================================
    // SERIALIZED FIELDS - UI REFERENCES
    // ========================================================================

    #region Serialized Fields
    [Header("=== HUD ELEMENTS ===")]
    [Tooltip("Timer display text")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Tooltip("Deaths counter display")]
    [SerializeField] private TextMeshProUGUI deathsText;

    [Tooltip("Escapes counter display")]
    [SerializeField] private TextMeshProUGUI escapesText;

    [Tooltip("Alerts counter display")]
    [SerializeField] private TextMeshProUGUI alertsText;

    [Tooltip("Current AI type display")]
    [SerializeField] private TextMeshProUGUI aiDisplayText;

    [Tooltip("Alert time display")]
    [SerializeField] private TextMeshProUGUI alertTimeText;

    [Tooltip("Retries counter display")]
    [SerializeField] private TextMeshProUGUI retriesText;

    [Tooltip("Top bar container GameObject")]
    [SerializeField] private GameObject topBar;

    [Header("=== VICTORY PANEL ===")]
    [Tooltip("Victory panel GameObject")]
    [SerializeField] private GameObject victoryPanel;

    [Tooltip("Victory panel timer text")]
    [SerializeField] private TextMeshProUGUI victoryTimerText;

    [Tooltip("Victory panel deaths text")]
    [SerializeField] private TextMeshProUGUI victoryDeathsText;

    [Tooltip("Victory panel alerts text")]
    [SerializeField] private TextMeshProUGUI victoryAlertsText;

    [Tooltip("Victory panel stealth score text")]
    [SerializeField] private TextMeshProUGUI victoryStealthScoreText;

    [Tooltip("Best time record display")]
    [SerializeField] private TextMeshProUGUI bestTimeText;

    [Tooltip("Best stealth score display")]
    [SerializeField] private TextMeshProUGUI bestStealthScoreText;

    [Tooltip("Replay button on victory panel")]
    [SerializeField] private Button victoryReplayButton;

    [Tooltip("Quit button on victory panel")]
    [SerializeField] private Button victoryQuitButton;

    [Tooltip("Start area time display")]
    [SerializeField] private TextMeshProUGUI startAreaTimeText;

    [Tooltip("Hut area time display")]
    [SerializeField] private TextMeshProUGUI hutAreaTimeText;

    [Tooltip("Maze area time display")]
    [SerializeField] private TextMeshProUGUI mazeAreaTimeText;

    [Tooltip("Last area time display")]
    [SerializeField] private TextMeshProUGUI lastAreaTimeText;

    [Tooltip("Total zone time display")]
    [SerializeField] private TextMeshProUGUI totalZoneTimeText;

    [Tooltip("Zone stats panel container")]
    [SerializeField] private GameObject zoneStatsPanel;

    [Header("=== GAME OVER PANEL ===")]
    [Tooltip("Game over panel GameObject")]
    [SerializeField] private GameObject gameOverPanel;

    [Tooltip("Game over timer text")]
    [SerializeField] private TextMeshProUGUI gameOverTimeText;

    [Tooltip("Game over alerts text")]
    [SerializeField] private TextMeshProUGUI gameOverAlertsText;

    [Tooltip("Game over retry button")]
    [SerializeField] private Button gameOverRetryButton;

    [Tooltip("Game over menu button")]
    [SerializeField] private Button gameOverMenuButton;

    [Tooltip("Game over start area time")]
    [SerializeField] private TextMeshProUGUI gameOverStartAreaTimeText;

    [Tooltip("Game over hut area time")]
    [SerializeField] private TextMeshProUGUI gameOverHutAreaTimeText;

    [Tooltip("Game over maze area time")]
    [SerializeField] private TextMeshProUGUI gameOverMazeAreaTimeText;

    [Tooltip("Game over last area time")]
    [SerializeField] private TextMeshProUGUI gameOverLastAreaTimeText;

    [Tooltip("Game over total zone time")]
    [SerializeField] private TextMeshProUGUI gameOverTotalZoneTimeText;

    [Tooltip("Game over zone stats panel")]
    [SerializeField] private GameObject gameOverZoneStatsPanel;

    [Header("=== SETTINGS ===")]
    [Tooltip("Name of the main menu scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    #endregion

    // ========================================================================
    // PUBLIC PROPERTIES
    // ========================================================================

    #region Properties
    public Player GetPlayer() => currentPlayer;
    public bool IsGameActive() => isGameActive;
    public float GetCurrentTime() => currentTime;
    public int GetDeathCount() => deathCount;
    public int GetAlertCount() => alertCount;

    /// <summary>
    /// Event triggered when a player is registered.
    /// Useful for systems that need player reference early.
    /// </summary>
    public System.Action<Player> OnPlayerRegistered;
    #endregion

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton pattern - ensure only one instance exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);  // Persist across scene loads
        }
        else
        {
            Destroy(gameObject);  // Destroy duplicate instances
            return;
        }
    }

    private void Start()
    {
        levelStartTime = Time.time;
        LoadBestRecords();          // Load saved personal bests
        FindUIReferences();         // Find all UI components
        AssignPlayerCamera();       // Assign camera to UI canvases
        DebugUI();                  // Output UI debug info
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void Update()
    {
        // Handle pause toggle (Escape key)
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            TogglePause();

        // Update timer and stealth score while game is active
        if (isGameActive && !isPaused)
        {
            currentTime = Time.time - levelStartTime;
            UpdateTimerUI();
            CalculateStealthScore();
        }
    }
    #endregion

    // ========================================================================
    // CAMERA MANAGEMENT
    // ========================================================================

    #region Camera Management
    /// <summary>
    /// Finds the player camera and assigns it to all UI canvases.
    /// Required for ScreenSpaceCamera canvases to render correctly.
    /// </summary>
    private void AssignPlayerCamera()
    {
        // Try to find Drive component (first-person controller)
        Drive drive = FindAnyObjectByType<Drive>();
        Camera playerCam = drive?.playerCamera ?? Camera.main;

        // Fallback: find by tag
        if (playerCam == null)
        {
            GameObject camObj = GameObject.FindGameObjectWithTag("MainCamera");
            playerCam = camObj?.GetComponent<Camera>();
        }

        // Assign camera to all canvases
        if (playerCam != null)
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                    canvas.worldCamera = playerCam;
            }
        }
        else
        {
            Debug.LogError("No player camera found for UI!");
        }
    }
    #endregion

    // ========================================================================
    // PLAYER REGISTRATION
    // ========================================================================

    #region Player Registration
    /// <summary>
    /// Registers the player with the GameManager.
    /// Called by Player component on Start.
    /// </summary>
    public void RegisterPlayer(Player player)
    {
        if (player == null)
        {
            Debug.LogError("Attempted to register null player!");
            return;
        }

        currentPlayer = player;

        // Set camera from player's Drive component
        Drive drive = player.GetComponent<Drive>();
        if (drive?.playerCamera != null)
            SetPlayerCamera(drive.playerCamera);

        // Notify all guards about the player
        NotifyGuardsOfPlayer();

        // Trigger registration event
        OnPlayerRegistered?.Invoke(player);
    }

    /// <summary>
    /// Sets the player camera and updates all UI canvases.
    /// </summary>
    private void SetPlayerCamera(Camera camera)
    {
        playerCamera = camera;
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                canvas.worldCamera = playerCamera;
        }
    }

    /// <summary>
    /// Notifies all guards about the player reference.
    /// Ensures guards can find player even if spawned after player.
    /// </summary>
    private void NotifyGuardsOfPlayer()
    {
        Guard[] guards = FindObjectsByType<Guard>(FindObjectsSortMode.None);
        foreach (var guard in guards)
        {
            if (guard?.currentBrain != null)
                guard.currentBrain.SetPlayer(currentPlayer);
        }
    }

    /// <summary>
    /// Refreshes guard references (called after scene load).
    /// </summary>
    public void RefreshGuardReferences()
    {
        if (currentPlayer != null) NotifyGuardsOfPlayer();
    }
    #endregion

    // ========================================================================
    // UI MANAGEMENT
    // ========================================================================

    #region UI Management
    /// <summary>
    /// Finds and caches all UI component references.
    /// Searches recursively through the canvas hierarchy.
    /// </summary>
    private void FindUIReferences()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("No canvas found in scene!");
            return;
        }

        // HUD Elements
        timerText = FindText(canvas.transform, "HUDTimerText");
        deathsText = FindText(canvas.transform, "HUDDeathsText");
        escapesText = FindText(canvas.transform, "HUDEscapesText");
        alertsText = FindText(canvas.transform, "HUDAlertText");
        aiDisplayText = FindText(canvas.transform, "AIDisplayText");
        alertTimeText = FindText(canvas.transform, "HUDAlertTime");
        retriesText = FindText(canvas.transform, "HUDRetriesText");
        topBar = FindChild(canvas.transform, "TopBar")?.gameObject;

        // Panels
        victoryPanel = FindChild(canvas.transform, "victoryPanel")?.gameObject;
        gameOverPanel = FindChild(canvas.transform, "gameOverPanel")?.gameObject;

        // Victory Panel Elements
        if (victoryPanel != null)
        {
            victoryTimerText = FindText(victoryPanel.transform, "victoryTimerText");
            victoryDeathsText = FindText(victoryPanel.transform, "victoryDeathsText");
            victoryAlertsText = FindText(victoryPanel.transform, "victoryAlertsText");
            victoryStealthScoreText = FindText(victoryPanel.transform, "victoryStealthScoreText");
            bestTimeText = FindText(victoryPanel.transform, "bestTimeText");
            bestStealthScoreText = FindText(victoryPanel.transform, "bestStealthScoreText");
            victoryReplayButton = FindButton(victoryPanel.transform, "victoryReplayButton");
            victoryQuitButton = FindButton(victoryPanel.transform, "victoryQuitButton");
            startAreaTimeText = FindText(victoryPanel.transform, "StartAreaTimeText");
            hutAreaTimeText = FindText(victoryPanel.transform, "HutAreaTimeText");
            mazeAreaTimeText = FindText(victoryPanel.transform, "MazeAreaTimeText");
            lastAreaTimeText = FindText(victoryPanel.transform, "LastAreaTimeText");
            totalZoneTimeText = FindText(victoryPanel.transform, "TotalZoneTimeText");
            zoneStatsPanel = FindChild(victoryPanel.transform, "ZoneStatsPanel")?.gameObject;
        }

        // Game Over Panel Elements
        if (gameOverPanel != null)
        {
            gameOverTimeText = FindText(gameOverPanel.transform, "gameOverTimeText");
            gameOverAlertsText = FindText(gameOverPanel.transform, "gameOverAlertsText");
            gameOverRetryButton = FindButton(gameOverPanel.transform, "gameOverRetryButton");
            gameOverMenuButton = FindButton(gameOverPanel.transform, "gameOverMenuButton");
            gameOverStartAreaTimeText = FindText(gameOverPanel.transform, "GameOverStartAreaTimeText");
            gameOverHutAreaTimeText = FindText(gameOverPanel.transform, "GameOverHutAreaTimeText");
            gameOverMazeAreaTimeText = FindText(gameOverPanel.transform, "GameOverMazeAreaTimeText");
            gameOverLastAreaTimeText = FindText(gameOverPanel.transform, "GameOverLastAreaTimeText");
            gameOverTotalZoneTimeText = FindText(gameOverPanel.transform, "GameOverTotalZoneTimeText");
            gameOverZoneStatsPanel = FindChild(gameOverPanel.transform, "GameOverZoneStatsPanel")?.gameObject;
        }

        // Hide panels initially
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    /// <summary>
    /// Helper: Finds a TextMeshProUGUI component by name.
    /// </summary>
    private TextMeshProUGUI FindText(Transform parent, string name)
    {
        Transform child = FindChildRecursive(parent, name);
        return child?.GetComponent<TextMeshProUGUI>();
    }

    /// <summary>
    /// Helper: Finds a Button component by name.
    /// </summary>
    private Button FindButton(Transform parent, string name)
    {
        Transform child = FindChildRecursive(parent, name);
        return child?.GetComponent<Button>();
    }

    /// <summary>
    /// Helper: Finds a child transform by name.
    /// </summary>
    private Transform FindChild(Transform parent, string name)
    {
        return FindChildRecursive(parent, name);
    }

    /// <summary>
    /// Recursively searches for a child transform by name.
    /// </summary>
    private Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform result = FindChildRecursive(child, name);
            if (result != null) return result;
        }
        return null;
    }
    #endregion

    // ========================================================================
    // UI UPDATES
    // ========================================================================

    #region UI Updates
    /// <summary>
    /// Debug method - outputs UI info to console.
    /// Useful for troubleshooting missing UI references.
    /// </summary>
    public void DebugUI()
    {
        // All debug logging is commented out to reduce console spam
        // Uncomment lines as needed for debugging
    }

    /// <summary>
    /// Updates the timer display in MM:SS format.
    /// </summary>
    private void UpdateTimerUI()
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(currentTime / 60);
            int seconds = Mathf.FloorToInt(currentTime % 60);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    /// <summary>
    /// Updates all HUD displays (deaths, alerts, escapes, AI type).
    /// </summary>
    private void UpdateAllUI()
    {
        if (deathsText != null) deathsText.text = $"Deaths: {deathCount}";
        if (alertsText != null) alertsText.text = $"Alerts: {alertCount}";
        if (escapesText != null) escapesText.text = $"Escapes: {escapeCount}";
        if (aiDisplayText != null && AISettings.Instance != null)
            aiDisplayText.text = $"Current AI: {AISettings.Instance.selectedAIType}";
    }

    /// <summary>
    /// Calculates the stealth score based on performance.
    /// Formula: 1000 - (alerts*50) - (spotted*25) - (deaths*200) - (time*2)
    /// Minimum score is 0.
    /// </summary>
    private void CalculateStealthScore()
    {
        stealthScore = Mathf.Max(0, 1000f - (alertCount * 50f) - (timesSpotted * 25f) - (deathCount * 200f) - (currentTime * 2f));
    }
    #endregion

    // ========================================================================
    // GAME EVENTS
    // ========================================================================

    #region Game Events
    /// <summary>
    /// Called when a guard loses sight of the player.
    /// </summary>
    public void GuardLostPlayer()
    {
        // Placeholder for future tracking
        Debug.Log("Guard lost the player");
    }

    /// <summary>
    /// Called when a guard becomes alert (detects something suspicious).
    /// Increments the alert counter.
    /// </summary>
    public void GuardAlerted()
    {
        if (!isGameActive) return;
        alertCount++;
        if (alertsText != null) alertsText.text = $"Alerts: {alertCount}";
    }

    /// <summary>
    /// Called when the player is spotted by a guard.
    /// Increments the spotted counter.
    /// </summary>
    public void PlayerSpotted()
    {
        if (!isGameActive) return;
        timesSpotted++;
    }

    /// <summary>
    /// Called when the player dies.
    /// Shows game over panel and increments death counter.
    /// </summary>
    public void PlayerDied(string cause)
    {
        if (!isGameActive) return;
        deathCount++;
        if (deathsText != null) deathsText.text = $"Deaths: {deathCount}";
        isGameActive = false;
        ShowGameOver();
    }

    /// <summary>
    /// Called when the player escapes the level.
    /// Shows victory panel and checks for new records.
    /// </summary>
    public void PlayerEscaped()
    {
        if (!isGameActive) return;
        escapeCount++;

        CheckNewRecords(currentTime, stealthScore, alertCount, deathCount);

        if (escapesText != null) escapesText.text = $"Escapes: {escapeCount}";

        ShowVictory(currentTime, stealthScore);
        isGameActive = false;
    }
    #endregion

    // ========================================================================
    // SCENE MANAGEMENT
    // ========================================================================

    #region Scene Management
    /// <summary>
    /// Called when a new scene is loaded.
    /// Resets game state but preserves retry count.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        int currentRetries = retries;
        FindUIReferences();
        ResetGame();
        retries = currentRetries;  // Preserve retry count

        if (retriesText != null) retriesText.text = $"Retries: {retries}";

        isPaused = false;
        Time.timeScale = 1f;

        DebugUI();

        if (currentPlayer != null)
            Invoke(nameof(RefreshGuardReferences), 0.1f);
    }

    /// <summary>
    /// Resets game statistics for a new run.
    /// </summary>
    private void ResetGame()
    {
        levelStartTime = Time.time;
        currentTime = 0f;
        alertCount = 0;
        timesSpotted = 0;
        stealthScore = 0f;
        isGameActive = true;

        ZoneManager.Instance?.ResetStats();

        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        UpdateAllUI();
        if (timerText != null) timerText.text = "00:00";
    }

    /// <summary>
    /// Restarts the current level.
    /// Increments retry counter and reloads the scene.
    /// </summary>
    public void RestartLevel()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        UnfreezeGame();
        retries++;

        ZoneManager.Instance?.ResetStats();

        isGameActive = true;
        deathCount = 0;
        escapeCount = 0;
        alertCount = 0;
        timesSpotted = 0;

        if (retriesText != null) retriesText.text = $"Retries: {retries}";

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Returns to the main menu.
    /// </summary>
    public void GoToMainMenu()
    {
        UnfreezeGame();
        ResetStats();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>
    /// Resets all statistics (called when returning to main menu).
    /// </summary>
    private void ResetStats()
    {
        deathCount = 0;
        alertCount = 0;
        timesSpotted = 0;
        escapeCount = 0;
        stealthScore = 0f;
        isGameActive = true;
        retries = 0;

        UpdateAllUI();
        if (retriesText != null) retriesText.text = $"Retries: {retries}";
    }
    #endregion

    // ========================================================================
    // PAUSE SYSTEM
    // ========================================================================

    #region Pause System
    /// <summary>
    /// Toggles between paused and resumed states.
    /// </summary>
    public void TogglePause()
    {
        if (isPaused) ResumeGame();
        else PauseGame();
    }

    /// <summary>
    /// Pauses the game - freezes time and shows pause UI.
    /// </summary>
    public void PauseGame()
    {
        if (!isGameActive) return;

        isPaused = true;
        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Reuse game over panel for pause menu
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (gameOverTimeText != null) gameOverTimeText.text = "PAUSED";
            if (gameOverAlertsText != null) gameOverAlertsText.text = "Game is paused";
            if (gameOverZoneStatsPanel != null) gameOverZoneStatsPanel.SetActive(false);

            // Configure retry button as resume
            if (gameOverRetryButton != null)
            {
                gameOverRetryButton.onClick.RemoveAllListeners();
                gameOverRetryButton.onClick.AddListener(ResumeGame);
                var btnText = gameOverRetryButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (btnText != null) btnText.text = "RESUME";
            }

            // Configure menu button
            if (gameOverMenuButton != null)
            {
                gameOverMenuButton.onClick.RemoveAllListeners();
                gameOverMenuButton.onClick.AddListener(GoToMainMenu);
                var btnText = gameOverMenuButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (btnText != null) btnText.text = "QUIT TO MENU";
            }
        }
    }

    /// <summary>
    /// Resumes the game from pause.
    /// </summary>
    public void ResumeGame()
    {
        if (!isPaused) return;

        isPaused = false;
        Time.timeScale = 1f;

        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// Freezes the game (for victory/game over screens).
    /// </summary>
    public void FreezeGame()
    {
        if (isGameFrozen) return;
        isGameFrozen = true;
        Time.timeScale = 0f;
    }

    /// <summary>
    /// Unfreezes the game.
    /// </summary>
    public void UnfreezeGame()
    {
        if (!isGameFrozen) return;
        isGameFrozen = false;
        Time.timeScale = 1f;
    }
    #endregion

    // ========================================================================
    // VICTORY & GAME OVER
    // ========================================================================

    #region Victory & Game Over
    /// <summary>
    /// Shows the game over panel with statistics.
    /// </summary>
    private void ShowGameOver()
    {
        if (gameOverPanel == null || isPaused) return;

        gameOverPanel.SetActive(true);
        FreezeGame();

        if (gameOverTimeText != null) gameOverTimeText.text = $"Time: {FormatTime(currentTime)}";
        if (gameOverAlertsText != null) gameOverAlertsText.text = $"Alerts: {alertCount}";

        UpdateGameOverZoneStatsDisplay();

        // Setup button listeners
        if (gameOverRetryButton != null)
        {
            gameOverRetryButton.onClick.RemoveAllListeners();
            gameOverRetryButton.onClick.AddListener(RestartLevel);
        }

        if (gameOverMenuButton != null)
        {
            gameOverMenuButton.onClick.RemoveAllListeners();
            gameOverMenuButton.onClick.AddListener(GoToMainMenu);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Shows the victory panel with statistics and record comparisons.
    /// </summary>
    private void ShowVictory(float time, float stealth)
    {
        if (victoryPanel == null) return;

        victoryPanel.SetActive(true);
        FreezeGame();

        // Display current run statistics
        if (victoryTimerText != null) victoryTimerText.text = $"Time: {FormatTime(time)}";
        if (victoryDeathsText != null) victoryDeathsText.text = $"Deaths: {deathCount}";
        if (victoryAlertsText != null) victoryAlertsText.text = $"Alerts: {alertCount}";
        if (victoryStealthScoreText != null) victoryStealthScoreText.text = $"Stealth Score: {stealth:F0}";

        // Display personal bests
        if (bestTimeText != null)
            bestTimeText.text = fastestEscape < Mathf.Infinity ? $"Best Time: {FormatTime(fastestEscape)}" : "Best Time: --:--";

        if (bestStealthScoreText != null)
            bestStealthScoreText.text = bestStealthRating > 0 ? $"Best Stealth: {bestStealthRating:F0}" : "Best Stealth: 0";

        // Display zone statistics
        UpdateZoneStatsDisplay();

        // Setup button listeners
        if (victoryReplayButton != null)
        {
            victoryReplayButton.onClick.RemoveAllListeners();
            victoryReplayButton.onClick.AddListener(RestartLevel);
        }

        if (victoryQuitButton != null)
        {
            victoryQuitButton.onClick.RemoveAllListeners();
            victoryQuitButton.onClick.AddListener(GoToMainMenu);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    #endregion

    // ========================================================================
    // ZONE STATS (Integration with ZoneManager)
    // ========================================================================

    #region Zone Stats
    /// <summary>
    /// Updates the victory panel with zone-based time statistics.
    /// </summary>
    private void UpdateZoneStatsDisplay()
    {
        if (ZoneManager.Instance == null) return;
        var stats = ZoneManager.Instance.GetZoneStats();

        if (startAreaTimeText != null) startAreaTimeText.text = GetZoneTime(stats, 1);
        if (hutAreaTimeText != null) hutAreaTimeText.text = GetZoneTime(stats, 2);
        if (mazeAreaTimeText != null) mazeAreaTimeText.text = GetZoneTime(stats, 3);
        if (lastAreaTimeText != null) lastAreaTimeText.text = GetZoneTime(stats, 4);

        float totalTime = 0f;
        foreach (var zone in stats) totalTime += zone.timeSpent;

        if (totalZoneTimeText != null) totalZoneTimeText.text = $"{totalTime:F2}s";
        if (zoneStatsPanel != null) zoneStatsPanel.SetActive(true);
    }

    /// <summary>
    /// Updates the game over panel with zone-based time statistics.
    /// </summary>
    private void UpdateGameOverZoneStatsDisplay()
    {
        if (ZoneManager.Instance == null) return;
        var stats = ZoneManager.Instance.GetZoneStats();

        if (gameOverStartAreaTimeText != null) gameOverStartAreaTimeText.text = GetZoneTime(stats, 1);
        if (gameOverHutAreaTimeText != null) gameOverHutAreaTimeText.text = GetZoneTime(stats, 2);
        if (gameOverMazeAreaTimeText != null) gameOverMazeAreaTimeText.text = GetZoneTime(stats, 3);
        if (gameOverLastAreaTimeText != null) gameOverLastAreaTimeText.text = GetZoneTime(stats, 4);

        float totalTime = 0f;
        foreach (var zone in stats) totalTime += zone.timeSpent;

        if (gameOverTotalZoneTimeText != null) gameOverTotalZoneTimeText.text = $"{totalTime:F1}s";
        if (gameOverZoneStatsPanel != null) gameOverZoneStatsPanel.SetActive(true);
    }

    /// <summary>
    /// Helper: Gets formatted time for a specific zone.
    /// </summary>
    private string GetZoneTime(List<ZoneManager.ZoneData> stats, int zoneNumber)
    {
        foreach (var zone in stats)
            if (zone.zoneNumber == zoneNumber)
                return $"{zone.timeSpent:F2}s";
        return "0.00s";
    }
    #endregion

    // ========================================================================
    // RECORDS MANAGEMENT (Persistent storage)
    // ========================================================================

    #region Records
    /// <summary>
    /// Checks if current run set any new personal records.
    /// </summary>
    private void CheckNewRecords(float time, float stealth, int alerts, int deaths)
    {
        bool newRecord = false;

        if (time < fastestEscape) { fastestEscape = time; newRecord = true; }
        if (stealth > bestStealthRating) { bestStealthRating = stealth; newRecord = true; }
        if (alerts < fewestAlerts) { fewestAlerts = alerts; newRecord = true; }
        if (deaths < fewestDeaths) { fewestDeaths = deaths; newRecord = true; }

        if (newRecord) SaveBestRecords();
    }

    /// <summary>
    /// Saves personal best records to PlayerPrefs.
    /// </summary>
    private void SaveBestRecords()
    {
        PlayerPrefs.SetFloat("FastestEscape", fastestEscape);
        PlayerPrefs.SetFloat("BestStealth", bestStealthRating);
        PlayerPrefs.SetInt("FewestAlerts", fewestAlerts);
        PlayerPrefs.SetInt("FewestDeaths", fewestDeaths);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Loads personal best records from PlayerPrefs.
    /// </summary>
    private void LoadBestRecords()
    {
        fastestEscape = PlayerPrefs.GetFloat("FastestEscape", Mathf.Infinity);
        bestStealthRating = PlayerPrefs.GetFloat("BestStealth", 0f);
        fewestAlerts = PlayerPrefs.GetInt("FewestAlerts", int.MaxValue);
        fewestDeaths = PlayerPrefs.GetInt("FewestDeaths", int.MaxValue);
    }
    #endregion

    // ========================================================================
    // UTILITY METHODS
    // ========================================================================

    #region Utility
    /// <summary>
    /// Formats time in seconds to MM:SS format.
    /// </summary>
    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60);
        return $"{minutes:00}:{seconds:00}";
    }

    /// <summary>
    /// Quits the game (works in editor and build).
    /// </summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Ensures time scale is reset when application quits.
    /// </summary>
    private void OnApplicationQuit() => Time.timeScale = 1f;
    #endregion
}