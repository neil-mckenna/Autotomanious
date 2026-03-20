using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    #region Properties, Fields


    // Singleton for easy access
    public static GameManager Instance { get; private set; }

    [Header("HUD Elements - Will be auto-found")]

    [SerializeField] private TextMeshProUGUI HUDTimerText;
    [SerializeField] private TextMeshProUGUI HUDDeathsText;
    [SerializeField] private TextMeshProUGUI HUDEscapesText;
    [SerializeField] private TextMeshProUGUI HUDAlertText;
    [SerializeField] private TextMeshProUGUI aiDisplayText;
    [SerializeField] private TextMeshProUGUI HUDAlertTimeText;
    [SerializeField] private TextMeshProUGUI HUDRetriesText;

    [SerializeField] private GameObject TopBar;


    [Header("Victory Panel - Will be auto-found")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TextMeshProUGUI victoryTimerText;
    [SerializeField] private TextMeshProUGUI victoryDeathsText;
    [SerializeField] private TextMeshProUGUI victoryAlertsText;
    [SerializeField] private TextMeshProUGUI victoryStealthScoreText;
    [SerializeField] private TextMeshProUGUI bestTimeText;
    [SerializeField] private TextMeshProUGUI bestStealthScoreText;
    [SerializeField] private Button victoryReplayButton;
    [SerializeField] private Button victoryQuitButton;

    [Header("Game Over Panel - Will be auto-found")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI gameOverTimeText;
    [SerializeField] private TextMeshProUGUI gameOverAlertsText;
    [SerializeField] private Button gameOverRetryButton;
    [SerializeField] private Button gameOverMenuButton;

    [Header("Game Over Panel - Zone Stats")]
    [SerializeField] private TextMeshProUGUI gameOverStartAreaTimeText;
    [SerializeField] private TextMeshProUGUI gameOverHutAreaTimeText;
    [SerializeField] private TextMeshProUGUI gameOverMazeAreaTimeText;
    [SerializeField] private TextMeshProUGUI gameOverLastAreaTimeText;
    [SerializeField] private TextMeshProUGUI gameOverTotalZoneTimeText;
    [SerializeField] private GameObject gameOverZoneStatsPanel;

    [Header("Stats Tracking")]
    [SerializeField] private float levelStartTime;
    [SerializeField] private float currentTime;
    [SerializeField] private int deathCount = 0;
    [SerializeField] private int escapeCount = 0;
    [SerializeField] private int alertCount = 0;
    [SerializeField] private int timesSpotted = 0;
    [SerializeField] private float stealthScore = 0f;

    [SerializeField] private int retries = 0;

    [SerializeField] private float totalAlertTime = 0f;
    //[SerializeField] private float currentAlertStartTime = 0f;

    [Header("Victory Panel - Zone Stats")]
    [SerializeField] private TextMeshProUGUI startAreaTimeText; 
    [SerializeField] private TextMeshProUGUI hutAreaTimeText;   
    [SerializeField] private TextMeshProUGUI mazeAreaTimeText;    
    [SerializeField] private TextMeshProUGUI lastAreaTimeText;     
    [SerializeField] private TextMeshProUGUI totalZoneTimeText;
    [SerializeField] private GameObject zoneStatsPanel;


    [Header("Best Records")]
    [SerializeField] private float fastestEscape = Mathf.Infinity;
    [SerializeField] private float bestStealthRating = 0f;
    [SerializeField] private int fewestAlerts = int.MaxValue;
    [SerializeField] private int fewestDeaths = int.MaxValue;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool isGameActive = true;
    private GameObject player;

    // Public getters
    public bool IsGameActive() => isGameActive;
    public float GetCurrentTime() => currentTime;
    public int GetDeathCount() => deathCount;
    public int GetAlertCount() => alertCount;

    #endregion

    #region Start

    private void Awake()
    {
        // Singleton setup
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
        FindPlayer();

        // Find UI elements
        FindUIReferences();

        //Debug.Log("=== GAME STARTED ===");
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    #endregion

    #region OnScene Event
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        //Debug.Log("Scene loaded - finding UI elements...");

        // Store current retries before reset
        int currentRetries = retries;

        // Find UI elements in the new scene
        FindUIReferences();

        // Reset game state 
        ResetGame();

        // Restore retries
        retries = currentRetries;

        // Update the UI with correct retry count
        if (HUDRetriesText != null)
            HUDRetriesText.text = $"Retries: {retries}";

        //Debug.Log($"Retries restored to: {retries}");
    }

    #endregion

    #region UI

    private void FindUIReferences()
    {
       // Debug.Log("=== Finding UI References ===");

        // Find the main canvas
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("No canvas found in scene!");
            return;
        }

        // Log all canvas children to help debug naming issues
        //Debug.Log("Canvas contains:");
        LogAllChildren(canvas.transform, 0);

        // ----- HUD ELEMENTS  ---
        // string sensistivity will break otherwise
 
        HUDTimerText = FindTextInChildren(canvas.transform, "HUDTimerText");
        HUDDeathsText = FindTextInChildren(canvas.transform, "HUDDeathsText");
        HUDEscapesText = FindTextInChildren(canvas.transform, "HUDEscapesText");
        HUDAlertText = FindTextInChildren(canvas.transform, "HUDAlertText");
        
        aiDisplayText = FindTextInChildren(canvas.transform, "AIDisplayText");
        HUDAlertTimeText = FindTextInChildren(canvas.transform, "HUDAlertTime");
        HUDRetriesText = FindTextInChildren(canvas.transform, "HUDRetriesText");

       
        // background for top menu
        TopBar = FindChildByName(canvas.transform, "TopBar")?.gameObject;

        // ----- UI PANELS -----
        victoryPanel = FindChildByName(canvas.transform, "victoryPanel")?.gameObject;
        gameOverPanel = FindChildByName(canvas.transform, "gameOverPanel")?.gameObject;

        // ----- VICTORY PANEL ELEMENTS -----
        if (victoryPanel != null)
        {
            victoryTimerText = FindTextInChildren(victoryPanel.transform, "victoryTimerText");
            victoryDeathsText = FindTextInChildren(victoryPanel.transform, "victoryDeathsText");
            victoryAlertsText = FindTextInChildren(victoryPanel.transform, "victoryAlertsText");
            victoryStealthScoreText = FindTextInChildren(victoryPanel.transform, "victoryStealthScoreText");
            bestTimeText = FindTextInChildren(victoryPanel.transform, "bestTimeText");
            bestStealthScoreText = FindTextInChildren(victoryPanel.transform, "bestStealthScoreText");

            victoryReplayButton = FindButtonInChildren(victoryPanel.transform, "victoryReplayButton");
            victoryQuitButton = FindButtonInChildren(victoryPanel.transform, "victoryQuitButton");


            startAreaTimeText = FindTextInChildren(victoryPanel.transform, "StartAreaTimeText");
            hutAreaTimeText = FindTextInChildren(victoryPanel.transform, "HutAreaTimeText");
            mazeAreaTimeText = FindTextInChildren(victoryPanel.transform, "MazeAreaTimeText");
            lastAreaTimeText = FindTextInChildren(victoryPanel.transform, "LastAreaTimeText");
            totalZoneTimeText = FindTextInChildren(victoryPanel.transform, "TotalZoneTimeText");
            zoneStatsPanel = FindChildByName(victoryPanel.transform, "ZoneStatsPanel")?.gameObject;
        }
        else
        {
            Debug.LogWarning("VictoryPanel not found in canvas");
        }

        // ----- GAME OVER PANEL ELEMENTS -----
        if (gameOverPanel != null)
        {
            gameOverTimeText = FindTextInChildren(gameOverPanel.transform, "gameOverTimeText");
            gameOverAlertsText = FindTextInChildren(gameOverPanel.transform, "gameOverAlertsText");

            gameOverRetryButton = FindButtonInChildren(gameOverPanel.transform, "gameOverRetryButton");
            gameOverMenuButton = FindButtonInChildren(gameOverPanel.transform, "gameOverMenuButton");

            gameOverStartAreaTimeText = FindTextInChildren(gameOverPanel.transform, "GameOverStartAreaTimeText");
            gameOverHutAreaTimeText = FindTextInChildren(gameOverPanel.transform, "GameOverHutAreaTimeText");
            gameOverMazeAreaTimeText = FindTextInChildren(gameOverPanel.transform, "GameOverMazeAreaTimeText");
            gameOverLastAreaTimeText = FindTextInChildren(gameOverPanel.transform, "GameOverLastAreaTimeText");
            gameOverTotalZoneTimeText = FindTextInChildren(gameOverPanel.transform, "GameOverTotalZoneTimeText");
            gameOverZoneStatsPanel = FindChildByName(gameOverPanel.transform, "GameOverZoneStatsPanel")?.gameObject;
        }
        else
        {
            Debug.LogWarning("GameOverPanel not found in canvas");
        }

        // Hide panels initially
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        // Report what was found
        //Debug.Log($"=== UI Find Results ===");

        //Debug.Log($"HUD: Timer={(HUDTimerText != null)}, Deaths={(HUDDeathsText != null)}, Alerts={(HUDAlertText != null)}");

       
        //Debug.Log($"Panels: Victory={(victoryPanel != null)}, GameOver={(gameOverPanel != null)}");
    }

    // Helper: Find a TextMeshProUGUI component in children by name
    private TextMeshProUGUI FindTextInChildren(Transform parent, string name)
    {
        Transform child = FindChildRecursive(parent, name);
        if (child != null)
        {
            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                //Debug.Log($"Found text: {child.name}");
                return text;
            }
        }
        Debug.LogWarning($"Text '{name}' not found");
        return null;
    }

    // Helper: Find a Button component in children by name
    private Button FindButtonInChildren(Transform parent, string name)
    {
        Transform child = FindChildRecursive(parent, name);
        if (child != null)
        {
            Button button = child.GetComponent<Button>();
            if (button != null)
            {
                //Debug.Log($"Found button: {child.name}");
                return button;
            }
        }
        Debug.LogWarning($"Button '{name}' not found");
        return null;
    }

    // Helper: Find child by name, recursivly
    private Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;

            Transform result = FindChildRecursive(child, name);
            if (result != null)
                return result;
        }
        return null;
    }

    // Helper: Find direct child by name 
    private Transform FindChildByName(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
        }
        return null;
    }

    // Helper: Log all children for debugging
    private void LogAllChildren(Transform parent, int depth)
    {
        string indent = new string(' ', depth * 2);
        foreach (Transform child in parent)
        {
            //Debug.Log($"{indent}- {child.name}");
            LogAllChildren(child, depth + 1);
        }
    }

    #endregion

    #region Find Player 

    // find teh player in hierachy
    private void FindPlayer()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            player = GameObject.Find("Player");
        }
    }
    #endregion

    #region Updates

    private void Update()
    {
        if (isGameActive)
        {
            currentTime = Time.time - levelStartTime;
            UpdateTimer();

            CalculateStealthScore();
        }
    }

    // convert value to realtime

    private void UpdateTimer()
    {
        int minutes = Mathf.FloorToInt(currentTime / 60);
        int seconds = Mathf.FloorToInt(currentTime % 60);
   
        if (HUDTimerText != null)
        {
            HUDTimerText.text = $"{minutes:00}:{seconds:00}";

        }        
    }

    private void CalculateStealthScore()
    {
        float baseScore = 1000f;
        float alertPenalty = alertCount * 50f;
        float spottedPenalty = timesSpotted * 25f;
        float deathPenalty = deathCount * 200f;
        float timePenalty = currentTime * 2f;

        stealthScore = baseScore - alertPenalty - spottedPenalty - deathPenalty - timePenalty;
        stealthScore = Mathf.Max(0, stealthScore);
    }

    private void UpdateAllUI()
    {


        if (HUDDeathsText != null) HUDDeathsText.text = $"Deaths: {deathCount}";
        if (HUDAlertText != null) HUDAlertText.text = $"Alerts: {alertCount}";
        if (HUDEscapesText != null) HUDEscapesText.text = $"Escapes: {escapeCount}";
        if (HUDTimerText != null) HUDTimerText.text = "00:00";

        if (aiDisplayText != null) aiDisplayText.text = $"Current AI: {AISettings.Instance.selectedAIType.ToString()}";

    }

    #endregion

    #region Zone Statistics

    private void UpdateZoneStatsDisplay()
    {
        if (ZoneManager.Instance == null) return;

        // Get all stats
        var stats = ZoneManager.Instance.GetZoneStats();

        // Update zone times using the formatted helper
        if (startAreaTimeText != null)
            startAreaTimeText.text = GetZoneTimeWithDecimal(stats, 1);

        if (hutAreaTimeText != null)
            hutAreaTimeText.text = GetZoneTimeWithDecimal(stats, 2);

        if (mazeAreaTimeText != null)
            mazeAreaTimeText.text = GetZoneTimeWithDecimal(stats, 3);

        if (lastAreaTimeText != null)
            lastAreaTimeText.text = GetZoneTimeWithDecimal(stats, 4);


        // Calculate total
        float totalTime = 0f;
        foreach (var zone in stats)
        {
            totalTime += zone.timeSpent;
        }

        if (totalZoneTimeText != null)
        {
            int minutes = Mathf.FloorToInt(totalTime / 60);
            int seconds = Mathf.FloorToInt(totalTime % 60);
            totalZoneTimeText.text = $"{totalTime:F2}s";
        }

        if (zoneStatsPanel != null)
            zoneStatsPanel.SetActive(true);
    }

    // for game over version
    private void UpdateGameOverZoneStatsDisplay()
    {
        if (ZoneManager.Instance == null) return;

        var stats = ZoneManager.Instance.GetZoneStats();

        if (gameOverStartAreaTimeText != null)
            gameOverStartAreaTimeText.text = GetZoneTimeWithDecimal(stats, 1);

        if (gameOverHutAreaTimeText != null)
            gameOverHutAreaTimeText.text = GetZoneTimeWithDecimal(stats, 2);

        if (gameOverMazeAreaTimeText != null)
            gameOverMazeAreaTimeText.text = GetZoneTimeWithDecimal(stats, 3);

        if (gameOverLastAreaTimeText != null)
            gameOverLastAreaTimeText.text = GetZoneTimeWithDecimal(stats, 4);

        float totalTime = 0f;
        foreach (var zone in stats)
        {
            totalTime += zone.timeSpent;
        }

        if (gameOverTotalZoneTimeText != null)
        {
            gameOverTotalZoneTimeText.text = $"{totalTime:F1}s";
        }

        if (gameOverZoneStatsPanel != null)
            gameOverZoneStatsPanel.SetActive(true);
    }


    private string GetZoneTimeWithDecimal(List<ZoneManager.ZoneData> stats, int zoneNumber)
    {
        foreach (var zone in stats)
        {
            if (zone.zoneNumber == zoneNumber)
            {
                return $"{zone.timeSpent:F2}s"; // Shows like "8.77s"
            }
        }
        return "0.00s";
    }

    public void ResetZoneStats()
    {
        if (ZoneManager.Instance != null)
            ZoneManager.Instance.ResetStats();
    }

    #endregion

    #region Guards

    // how many time spotted
    public void GuardAlerted()
    {
        if (!isGameActive) return;

        alertCount++;
        Debug.Log($"Guard alerted! Total alerts: {alertCount}");

        if (HUDAlertText != null)
            HUDAlertText.text = $"Alerts: {alertCount}";
    }

    public void GuardLostPlayer()
    {

    }

    #endregion

    #region Player

    // time amount spotted
    public void PlayerSpotted()
    {
        if (!isGameActive) return;
        timesSpotted++;
    }

    public void PlayerDied(string cause)
    {
        if (!isGameActive) return;

        deathCount++;
        Debug.Log($"Player died: {cause}. Death #{deathCount}");



        if (HUDDeathsText != null)
            HUDDeathsText.text = $"Deaths: {deathCount}";

        isGameActive = false;

        ShowGameOver();
    }

    public void PlayerEscaped()
    {
        if (!isGameActive) return;

        PrintZoneStats();

        escapeCount++;
        Debug.Log($"PLAYER ESCAPED! Escape #{escapeCount}");

        float finalTime = currentTime;
        float finalStealth = stealthScore;

        Debug.Log($"Time: {finalTime:F2} seconds");
        Debug.Log($"Stealth Score: {finalStealth:F0}");
        Debug.Log($"Alerts: {alertCount}");
        Debug.Log($"Deaths: {deathCount}");

        CheckNewRecords(finalTime, finalStealth, alertCount, deathCount);


        if (HUDEscapesText != null)
            HUDEscapesText.text = $"Escapes: {escapeCount}";

        ShowVictory(finalTime, finalStealth);

        isGameActive = false;
    }

    public void PrintZoneStats()
    {
        if (ZoneManager.Instance != null)
            ZoneManager.Instance.PrintStats();
    }

    #endregion

    #region Scores, Saves and UI

    private void CheckNewRecords(float time, float stealth, int alerts, int deaths)
    {
        bool newRecord = false;

        if (time < fastestEscape)
        {
            fastestEscape = time;
            Debug.Log($"NEW RECORD! Fastest escape: {FormatTime(fastestEscape)}");
            newRecord = true;
        }

        if (stealth > bestStealthRating)
        {
            bestStealthRating = stealth;
            Debug.Log($"NEW RECORD! Best stealth: {bestStealthRating:F0}");
            newRecord = true;
        }

        if (alerts < fewestAlerts)
        {
            fewestAlerts = alerts;
            Debug.Log($"NEW RECORD! Fewest alerts: {fewestAlerts}");
            newRecord = true;
        }

        if (deaths < fewestDeaths)
        {
            fewestDeaths = deaths;
            Debug.Log($"NEW RECORD! Fewest deaths: {fewestDeaths}");
            newRecord = true;
        }

        if (newRecord)
        {
            SaveBestRecords();
        }
    }

    private void ShowGameOver()
    {
        if (gameOverPanel == null)
        {
            Debug.LogError("GameOverPanel is null! Can't show panel.");
            return;
        }

        gameOverPanel.SetActive(true);

        FreezeGame();

        if (gameOverTimeText != null)
            gameOverTimeText.text = $"Time: {FormatTime(currentTime)}";

        if (gameOverAlertsText != null)
            gameOverAlertsText.text = $"Alerts: {alertCount}";

        // duplicate as end method
        UpdateGameOverZoneStatsDisplay();

        if (gameOverRetryButton != null)
        {
            gameOverRetryButton.onClick.RemoveAllListeners();
            gameOverRetryButton.onClick.AddListener(RestartGame);
        }

        if (gameOverMenuButton != null)
        {
            gameOverMenuButton.onClick.RemoveAllListeners();
            gameOverMenuButton.onClick.AddListener(GoToMainMenu);
        }

        isGameActive = false;
    }

    private void ShowVictory(float time, float stealth)
    {
        if (victoryPanel == null) return;

        victoryPanel.SetActive(true);

        FreezeGame();

        string timeString = FormatTime(time);

        if (victoryTimerText != null)
            victoryTimerText.text = $"Time: {timeString}";

        if (victoryDeathsText != null)
            victoryDeathsText.text = $"Deaths: {deathCount}";

        if (victoryAlertsText != null)
            victoryAlertsText.text = $"Alerts: {alertCount}";

        if (victoryStealthScoreText != null)
            victoryStealthScoreText.text = $"Stealth Score: {stealth:F0}";

        if (bestTimeText != null)
        {
            if (fastestEscape < Mathf.Infinity)
                bestTimeText.text = $"Best Time: {FormatTime(fastestEscape)}";
            else
                bestTimeText.text = "Best Time: --:--";
        }

        if (bestStealthScoreText != null)
        {
            if (bestStealthRating > 0)
                bestStealthScoreText.text = $"Best Stealth: {bestStealthRating:F0}";
            else
                bestStealthScoreText.text = "Best Stealth: 0";
        }

        // call zones
        UpdateZoneStatsDisplay();

        if (victoryReplayButton != null)
        {
            victoryReplayButton.onClick.RemoveAllListeners();
            victoryReplayButton.onClick.AddListener(RestartGame);
        }

        if (victoryQuitButton != null)
        {
            victoryQuitButton.onClick.RemoveAllListeners();
            victoryQuitButton.onClick.AddListener(GoToMainMenu);
        }
    }

    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60);
        return $"{minutes:00}:{seconds:00}";
    }

    private void SaveBestRecords()
    {
        PlayerPrefs.SetFloat("FastestEscape", fastestEscape);
        PlayerPrefs.SetFloat("BestStealth", bestStealthRating);
        PlayerPrefs.SetInt("FewestAlerts", fewestAlerts);
        PlayerPrefs.SetInt("FewestDeaths", fewestDeaths);
        PlayerPrefs.Save();

        Debug.Log("Records saved to PlayerPrefs!");
    }

    private void LoadBestRecords()
    {
        fastestEscape = PlayerPrefs.GetFloat("FastestEscape", Mathf.Infinity);
        bestStealthRating = PlayerPrefs.GetFloat("BestStealth", 0f);
        fewestAlerts = PlayerPrefs.GetInt("FewestAlerts", int.MaxValue);
        fewestDeaths = PlayerPrefs.GetInt("FewestDeaths", int.MaxValue);

        //Debug.Log("Records loaded from PlayerPrefs");
    }

    public void ResetBestScores()
    {
        // Reset to default values
        fastestEscape = Mathf.Infinity;
        bestStealthRating = 0f;
        fewestAlerts = int.MaxValue;
        fewestDeaths = int.MaxValue;

        // Save the reset values
        SaveBestRecords();

        Debug.Log("Best scores have been reset!");

        // 
        if (bestTimeText != null)
            bestTimeText.text = "Best Time: --:--";

        if (bestStealthScoreText != null)
            bestStealthScoreText.text = "Best Stealth: 0";
    }

    [ContextMenu("Reset Best Scores")]
    public void ContextResetBestScores()
    {
        ResetBestScores();
    }

    #endregion

    #region Resets, Navigation

    // hard reset game 
    private void ResetGame()
    {
        //Debug.Log("Resetting Game State");

        levelStartTime = Time.time;
        currentTime = 0f;

        alertCount = 0;
        timesSpotted = 0;
        stealthScore = 0f;
        isGameActive = true;

        if (ZoneManager.Instance != null)
        {
            ZoneManager.Instance.ResetStats();
            Debug.Log("Zone stats reset for new playthrough");
        }

        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        FindPlayer();
        UpdateAllUI();

        if (HUDTimerText != null) HUDTimerText.text = "00:00";

    }

    

    public void RestartLevel()
    {
        UnfreezeGame();

        Debug.Log($"RestartLevel called. Current retries: {retries}");
        retries++;
        Debug.Log($"Retries now: {retries}");

        if (ZoneManager.Instance != null)
        {
            ZoneManager.Instance.ResetStats();
            Debug.Log("Zone stats reset for new playthrough");
        }

        if (HUDRetriesText != null)
            HUDRetriesText.text = $"Retries: {retries}";

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);


    }

    public void GoToMainMenu()
    {
        UnfreezeGame();

        // reset deaths
        deathCount = 0;
        alertCount = 0;
        timesSpotted = 0;
        escapeCount = 0;
        stealthScore = 0f;
        isGameActive = true;

        // retries
        retries = 0;

        if (HUDDeathsText != null)
            HUDDeathsText.text = $"Deaths: {deathCount}";

        if (HUDAlertText != null)
            HUDAlertText.text = $"Alerts: {alertCount}";

        if (HUDEscapesText != null)
            HUDEscapesText.text = $"Escapes: {escapeCount}";

        if (HUDRetriesText != null)
            HUDRetriesText.text = $"Retries: {retries}";

        SceneManager.LoadScene(mainMenuSceneName);
    }

    #region Time Control

    private bool isGameFrozen = false;

    public void FreezeGame()
    {
        if (isGameFrozen) return;

        isGameFrozen = true;
        Time.timeScale = 0f;
        Debug.Log("Game time frozen");
    }

    public void UnfreezeGame()
    {
        if (!isGameFrozen) return;

        isGameFrozen = false;
        Time.timeScale = 1f;
        Debug.Log("Game time unfrozen");
    }

    public void RestartGame()
    {
        UnfreezeGame();  // Ensure time is normal before reloading
        RestartLevel();
    }

    #endregion

    private void OnApplicationQuit()
    {
        Time.timeScale = 1f;
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

}