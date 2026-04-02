using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    #region Singleton
    public static GameManager Instance { get; private set; }
    #endregion

    #region Fields
    private Player currentPlayer;
    private Camera playerCamera;
    private bool isGameActive = true;
    private bool isPaused = false;
    private bool isGameFrozen = false;

    // Stats
    private float levelStartTime;
    private float currentTime;
    private int deathCount = 0;
    private int escapeCount = 0;
    private int alertCount = 0;
    private int timesSpotted = 0;
    private float stealthScore = 0f;
    private int retries = 0;
    private float totalAlertTime = 0f;

    // Records
    private float fastestEscape = Mathf.Infinity;
    private float bestStealthRating = 0f;
    private int fewestAlerts = int.MaxValue;
    private int fewestDeaths = int.MaxValue;
    #endregion

    #region Serialized Fields
    [Header("HUD Elements")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI deathsText;
    [SerializeField] private TextMeshProUGUI escapesText;
    [SerializeField] private TextMeshProUGUI alertsText;
    [SerializeField] private TextMeshProUGUI aiDisplayText;
    [SerializeField] private TextMeshProUGUI alertTimeText;
    [SerializeField] private TextMeshProUGUI retriesText;
    [SerializeField] private GameObject topBar;

    [Header("Victory Panel")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TextMeshProUGUI victoryTimerText;
    [SerializeField] private TextMeshProUGUI victoryDeathsText;
    [SerializeField] private TextMeshProUGUI victoryAlertsText;
    [SerializeField] private TextMeshProUGUI victoryStealthScoreText;
    [SerializeField] private TextMeshProUGUI bestTimeText;
    [SerializeField] private TextMeshProUGUI bestStealthScoreText;
    [SerializeField] private Button victoryReplayButton;
    [SerializeField] private Button victoryQuitButton;
    [SerializeField] private TextMeshProUGUI startAreaTimeText;
    [SerializeField] private TextMeshProUGUI hutAreaTimeText;
    [SerializeField] private TextMeshProUGUI mazeAreaTimeText;
    [SerializeField] private TextMeshProUGUI lastAreaTimeText;
    [SerializeField] private TextMeshProUGUI totalZoneTimeText;
    [SerializeField] private GameObject zoneStatsPanel;

    [Header("Game Over Panel")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI gameOverTimeText;
    [SerializeField] private TextMeshProUGUI gameOverAlertsText;
    [SerializeField] private Button gameOverRetryButton;
    [SerializeField] private Button gameOverMenuButton;
    [SerializeField] private TextMeshProUGUI gameOverStartAreaTimeText;
    [SerializeField] private TextMeshProUGUI gameOverHutAreaTimeText;
    [SerializeField] private TextMeshProUGUI gameOverMazeAreaTimeText;
    [SerializeField] private TextMeshProUGUI gameOverLastAreaTimeText;
    [SerializeField] private TextMeshProUGUI gameOverTotalZoneTimeText;
    [SerializeField] private GameObject gameOverZoneStatsPanel;

    [Header("Settings")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    #endregion

    #region Properties
    public Player GetPlayer() => currentPlayer;
    public bool IsGameActive() => isGameActive;
    public float GetCurrentTime() => currentTime;
    public int GetDeathCount() => deathCount;
    public int GetAlertCount() => alertCount;
    public System.Action<Player> OnPlayerRegistered;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        levelStartTime = Time.time;
        LoadBestRecords();
        FindUIReferences();
        AssignPlayerCamera();
        DebugUI();
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            TogglePause();

        if (isGameActive && !isPaused)
        {
            currentTime = Time.time - levelStartTime;
            UpdateTimerUI();
            CalculateStealthScore();
        }
    }
    #endregion

    #region Camera Management
    private void AssignPlayerCamera()
    {
        Drive drive = FindAnyObjectByType<Drive>();
        Camera playerCam = drive?.playerCamera ?? Camera.main;

        if (playerCam == null)
        {
            GameObject camObj = GameObject.FindGameObjectWithTag("MainCamera");
            playerCam = camObj?.GetComponent<Camera>();
        }

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

    #region Player Registration
    public void RegisterPlayer(Player player)
    {
        if (player == null)
        {
            Debug.LogError("Attempted to register null player!");
            return;
        }

        currentPlayer = player;

        Drive drive = player.GetComponent<Drive>();
        if (drive?.playerCamera != null)
            SetPlayerCamera(drive.playerCamera);

        NotifyGuardsOfPlayer();
        OnPlayerRegistered?.Invoke(player);
    }

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

    private void NotifyGuardsOfPlayer()
    {
        Guard[] guards = FindObjectsByType<Guard>(FindObjectsSortMode.None);
        foreach (var guard in guards)
        {
            if (guard?.currentBrain != null)
                guard.currentBrain.SetPlayer(currentPlayer);
        }
    }

    public void RefreshGuardReferences()
    {
        if (currentPlayer != null) NotifyGuardsOfPlayer();
    }
    #endregion

    #region UI Management
    private void FindUIReferences()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("No canvas found in scene!");
            return;
        }

        // HUD
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

    private TextMeshProUGUI FindText(Transform parent, string name)
    {
        Transform child = FindChildRecursive(parent, name);
        return child?.GetComponent<TextMeshProUGUI>();
    }

    private Button FindButton(Transform parent, string name)
    {
        Transform child = FindChildRecursive(parent, name);
        return child?.GetComponent<Button>();
    }

    private Transform FindChild(Transform parent, string name)
    {
        return FindChildRecursive(parent, name);
    }

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

    #region UI Updates

    public void DebugUI()
    {
        Debug.Log("=== UI DEBUG ===");

        // Check Canvas
        Canvas canvas = FindAnyObjectByType<Canvas>();
        Debug.Log($"Canvas found: {canvas != null}");
        if (canvas != null)
        {
            Debug.Log($"Canvas name: {canvas.name}");
            Debug.Log($"Canvas render mode: {canvas.renderMode}");
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                Debug.Log($"Canvas world camera: {canvas.worldCamera?.name ?? "NULL"}");
            }
        }

        // Check HUD elements
        Debug.Log($"Timer Text: {timerText != null}");
        Debug.Log($"Deaths Text: {deathsText != null}");
        Debug.Log($"Escapes Text: {escapesText != null}");
        Debug.Log($"Alerts Text: {alertsText != null}");
        Debug.Log($"Retries Text: {retriesText != null}");

        // Check Panels
        Debug.Log($"Victory Panel: {victoryPanel != null}");
        Debug.Log($"Game Over Panel: {gameOverPanel != null}");

        // Check if UI is active
        if (timerText != null)
            Debug.Log($"Timer text value: {timerText.text}");

        // List all TextMeshPro objects in scene
        TextMeshProUGUI[] allTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
        Debug.Log($"Total TextMeshPro objects in scene: {allTexts.Length}");
        foreach (var text in allTexts)
        {
            Debug.Log($"  - {text.name} (active: {text.gameObject.activeSelf})");
        }
    }
    private void UpdateTimerUI()
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(currentTime / 60);
            int seconds = Mathf.FloorToInt(currentTime % 60);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    private void UpdateAllUI()
    {
        if (deathsText != null) deathsText.text = $"Deaths: {deathCount}";
        if (alertsText != null) alertsText.text = $"Alerts: {alertCount}";
        if (escapesText != null) escapesText.text = $"Escapes: {escapeCount}";
        if (aiDisplayText != null && AISettings.Instance != null)
            aiDisplayText.text = $"Current AI: {AISettings.Instance.selectedAIType}";
    }

    private void CalculateStealthScore()
    {
        stealthScore = Mathf.Max(0, 1000f - (alertCount * 50f) - (timesSpotted * 25f) - (deathCount * 200f) - (currentTime * 2f));
    }
    #endregion

    #region Game Events

    public void GuardLostPlayer()
    {
        // Can be used for tracking when guards lose sight of player
        // For now, just a placeholder
        Debug.Log("Guard lost the player");
    }
    public void GuardAlerted()
    {
        if (!isGameActive) return;
        alertCount++;
        if (alertsText != null) alertsText.text = $"Alerts: {alertCount}";
    }

    public void PlayerSpotted()
    {
        if (!isGameActive) return;
        timesSpotted++;
    }

    public void PlayerDied(string cause)
    {
        if (!isGameActive) return;
        deathCount++;
        if (deathsText != null) deathsText.text = $"Deaths: {deathCount}";
        isGameActive = false;
        ShowGameOver();
    }

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

    #region Scene Management
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        int currentRetries = retries;
        FindUIReferences();
        ResetGame();
        retries = currentRetries;

        if (retriesText != null) retriesText.text = $"Retries: {retries}";

        isPaused = false;
        Time.timeScale = 1f;

        DebugUI();

        if (currentPlayer != null)
            Invoke(nameof(RefreshGuardReferences), 0.1f);
    }

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

    public void GoToMainMenu()
    {
        UnfreezeGame();
        ResetStats();
        SceneManager.LoadScene(mainMenuSceneName);
    }

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

    #region Pause System
    public void TogglePause()
    {
        if (isPaused) ResumeGame();
        else PauseGame();
    }

    public void PauseGame()
    {
        if (!isGameActive) return;

        isPaused = true;
        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (gameOverTimeText != null) gameOverTimeText.text = "PAUSED";
            if (gameOverAlertsText != null) gameOverAlertsText.text = "Game is paused";
            if (gameOverZoneStatsPanel != null) gameOverZoneStatsPanel.SetActive(false);

            if (gameOverRetryButton != null)
            {
                gameOverRetryButton.onClick.RemoveAllListeners();
                gameOverRetryButton.onClick.AddListener(ResumeGame);
                var btnText = gameOverRetryButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (btnText != null) btnText.text = "RESUME";
            }

            if (gameOverMenuButton != null)
            {
                gameOverMenuButton.onClick.RemoveAllListeners();
                gameOverMenuButton.onClick.AddListener(GoToMainMenu);
                var btnText = gameOverMenuButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (btnText != null) btnText.text = "QUIT TO MENU";
            }
        }
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        isPaused = false;
        Time.timeScale = 1f;

        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void FreezeGame()
    {
        if (isGameFrozen) return;
        isGameFrozen = true;
        Time.timeScale = 0f;
    }

    public void UnfreezeGame()
    {
        if (!isGameFrozen) return;
        isGameFrozen = false;
        Time.timeScale = 1f;
    }
    #endregion

    #region Victory & Game Over
    private void ShowGameOver()
    {
        if (gameOverPanel == null || isPaused) return;

        gameOverPanel.SetActive(true);
        FreezeGame();

        if (gameOverTimeText != null) gameOverTimeText.text = $"Time: {FormatTime(currentTime)}";
        if (gameOverAlertsText != null) gameOverAlertsText.text = $"Alerts: {alertCount}";

        UpdateGameOverZoneStatsDisplay();

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

    private void ShowVictory(float time, float stealth)
    {
        if (victoryPanel == null) return;

        victoryPanel.SetActive(true);
        FreezeGame();

        if (victoryTimerText != null) victoryTimerText.text = $"Time: {FormatTime(time)}";
        if (victoryDeathsText != null) victoryDeathsText.text = $"Deaths: {deathCount}";
        if (victoryAlertsText != null) victoryAlertsText.text = $"Alerts: {alertCount}";
        if (victoryStealthScoreText != null) victoryStealthScoreText.text = $"Stealth Score: {stealth:F0}";

        if (bestTimeText != null)
            bestTimeText.text = fastestEscape < Mathf.Infinity ? $"Best Time: {FormatTime(fastestEscape)}" : "Best Time: --:--";

        if (bestStealthScoreText != null)
            bestStealthScoreText.text = bestStealthRating > 0 ? $"Best Stealth: {bestStealthRating:F0}" : "Best Stealth: 0";

        UpdateZoneStatsDisplay();

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

    #region Zone Stats
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

    private string GetZoneTime(List<ZoneManager.ZoneData> stats, int zoneNumber)
    {
        foreach (var zone in stats)
            if (zone.zoneNumber == zoneNumber)
                return $"{zone.timeSpent:F2}s";
        return "0.00s";
    }
    #endregion

    #region Records
    private void CheckNewRecords(float time, float stealth, int alerts, int deaths)
    {
        bool newRecord = false;

        if (time < fastestEscape) { fastestEscape = time; newRecord = true; }
        if (stealth > bestStealthRating) { bestStealthRating = stealth; newRecord = true; }
        if (alerts < fewestAlerts) { fewestAlerts = alerts; newRecord = true; }
        if (deaths < fewestDeaths) { fewestDeaths = deaths; newRecord = true; }

        if (newRecord) SaveBestRecords();
    }

    private void SaveBestRecords()
    {
        PlayerPrefs.SetFloat("FastestEscape", fastestEscape);
        PlayerPrefs.SetFloat("BestStealth", bestStealthRating);
        PlayerPrefs.SetInt("FewestAlerts", fewestAlerts);
        PlayerPrefs.SetInt("FewestDeaths", fewestDeaths);
        PlayerPrefs.Save();
    }

    private void LoadBestRecords()
    {
        fastestEscape = PlayerPrefs.GetFloat("FastestEscape", Mathf.Infinity);
        bestStealthRating = PlayerPrefs.GetFloat("BestStealth", 0f);
        fewestAlerts = PlayerPrefs.GetInt("FewestAlerts", int.MaxValue);
        fewestDeaths = PlayerPrefs.GetInt("FewestDeaths", int.MaxValue);
    }
    #endregion

    #region Utility
    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60);
        return $"{minutes:00}:{seconds:00}";
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnApplicationQuit() => Time.timeScale = 1f;
    #endregion
}