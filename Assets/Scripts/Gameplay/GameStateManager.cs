using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class GameStateManager : MonoBehaviour
{
    public enum GameFlowState
    {
        Intro = -1,
        Playing = 0,
        Paused = 1,
        Victory = 2,
        Defeat = 3
    }

    [Header("References")]
    [SerializeField] private ZombieWaveSpawner waveSpawner;
    [SerializeField] private FlowerEntity[] trackedFlowers;
    [SerializeField] private bool autoFindFlowers = true;
    [SerializeField] private bool autoStartWaves = true;

    [Header("Pause")]
    [SerializeField] private bool allowPause = true;
    [SerializeField] private Key pauseKey = Key.Escape;
    [SerializeField] private bool freezeTimeOnPause = true;

    [Header("End States")]
    [SerializeField] private bool freezeTimeOnVictoryOrDefeat = true;
    [FormerlySerializedAs("checkDefeatWhenAllFlowersDead")]
    [SerializeField] private bool checkDefeatWhenAnyFlowerDies = true;
    [SerializeField, Min(0f)] private float victoryDelaySeconds = 1f;
    [SerializeField, Min(0f)] private float defeatDelaySeconds = 1f;
    [SerializeField] private bool useUnscaledEndStateDelay = true;

    [Header("UI Screens")]
    [SerializeField] private GameObject pauseScreen;
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private GameObject defeatScreen;

    [Header("Level Thanks")]
    [SerializeField] private bool showLevelThanks;
    [SerializeField] private GameObject levelThanksScreen;

    [Header("Level Intro")]
    [FormerlySerializedAs("showFirstLevelIntro")]
    [SerializeField] private bool showLevelIntro = true;
    [FormerlySerializedAs("firstLevelIntroScreen")]
    [SerializeField] private GameObject levelIntroScreen;

    [Header("Levels")]
    [SerializeField, Min(1)] private int totalLevels = 5;
    [SerializeField] private string[] levelSceneNames;
    [SerializeField] private bool useBuildIndexProgression = true;
    [SerializeField, Min(0)] private int firstLevelBuildIndex = 0;

    private GameFlowState currentState = GameFlowState.Playing;
    private Coroutine pendingEndStateRoutine;
    private GameFlowState? pendingEndState;
    private bool isLevelIntroActive;
    private bool isLevelThanksActive;
    private bool isRegisteredGameplayBlock;
    private bool isRegisteredIntroState;

    private static int gameplayBlockersCount;
    private static int introStateCount;

    public static bool IsGameplayInputBlocked { get; private set; }
    public static bool IsAnyIntroActive => introStateCount > 0;

    public GameFlowState CurrentState => currentState;
    public int TotalFlowerCount => trackedFlowers != null ? trackedFlowers.Length : 0;
    public int AliveFlowerCount => CountAliveFlowers();
    public event Action<GameFlowState> StateChanged;

    private void Awake()
    {
        if (waveSpawner == null)
        {
            waveSpawner = FindFirstObjectByType<ZombieWaveSpawner>();
        }

        ResolveFlowers();
        InitializeLevelIntro();
        SetState(isLevelIntroActive ? GameFlowState.Intro : GameFlowState.Playing, true);
    }

    private void OnEnable()
    {
        SubscribeWaveSpawner();
        SubscribeFlowers();

        if (autoStartWaves && waveSpawner != null && !waveSpawner.IsRunning && currentState == GameFlowState.Playing && !IsAnyIntroActive)
        {
            waveSpawner.StartSpawning();
        }
    }

    private void OnDisable()
    {
        UnsubscribeWaveSpawner();
        UnsubscribeFlowers();
        ClearPendingEndState();
        SetGameplayBlockedByThis(false);
        SetIntroStateByThis(false);

        if (!IsGameplayInputBlocked && currentState != GameFlowState.Victory && currentState != GameFlowState.Defeat)
        {
            Time.timeScale = 1f;
        }
    }

    private void Update()
    {
        if (IsAnyIntroActive)
        {
            if (Time.timeScale != 0f)
            {
                Time.timeScale = 0f;
            }

            SetScreenState(pauseScreen, false);
            return;
        }

        if (!allowPause)
        {
            return;
        }

        if (currentState != GameFlowState.Playing && currentState != GameFlowState.Paused)
        {
            return;
        }

        if (!IsPausePressedThisFrame())
        {
            return;
        }

        TogglePause();
    }

    public void TogglePause()
    {
        if (IsAnyIntroActive)
        {
            return;
        }

        if (currentState == GameFlowState.Playing)
        {
            SetState(GameFlowState.Paused);
            return;
        }

        if (currentState == GameFlowState.Paused)
        {
            SetState(GameFlowState.Playing);
        }
    }

    public void PauseGame()
    {
        if (IsAnyIntroActive)
        {
            return;
        }

        if (currentState == GameFlowState.Playing)
        {
            SetState(GameFlowState.Paused);
        }
    }

    public void ResumeGame()
    {
        if (IsAnyIntroActive)
        {
            return;
        }

        if (currentState == GameFlowState.Paused)
        {
            SetState(GameFlowState.Playing);
        }
    }

    public void CloseTutorial()
    {
        if (currentState != GameFlowState.Intro)
        {
            return;
        }

        isLevelIntroActive = false;
        SetState(GameFlowState.Playing);

        if (autoStartWaves && waveSpawner != null && !waveSpawner.IsRunning && !IsAnyIntroActive)
        {
            waveSpawner.StartSpawning();
        }
    }

    public void StartLevelFromIntro()
    {
        if (IsAnyIntroActive)
        {
#if UNITY_2023_1_OR_NEWER
            GameStateManager[] managers = FindObjectsByType<GameStateManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            GameStateManager[] managers = FindObjectsOfType<GameStateManager>(true);
#endif
            for (int i = 0; i < managers.Length; i++)
            {
                GameStateManager manager = managers[i];
                if (manager == null)
                {
                    continue;
                }

                manager.CloseTutorial();
            }

            return;
        }

        CloseTutorial();
    }

    public void RestartCurrentLevel()
    {
        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    public void CloseGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void StartFromFirstLevel()
    {
        if (!TryGetFirstLevel(out string firstSceneName, out int firstBuildIndex))
        {
            Debug.LogWarning("First level is not configured. Restarting current level.", this);
            RestartCurrentLevel();
            return;
        }

        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(firstSceneName))
        {
            SceneManager.LoadScene(firstSceneName);
            return;
        }

        SceneManager.LoadScene(firstBuildIndex);
    }

    public void LoadNextLevel()
    {
        if (TryGetNextLevel(out string nextSceneName, out int nextBuildIndex))
        {
            Time.timeScale = 1f;
            if (!string.IsNullOrEmpty(nextSceneName))
            {
                SceneManager.LoadScene(nextSceneName);
                return;
            }

            SceneManager.LoadScene(nextBuildIndex);
            return;
        }

        Debug.Log("No next level configured. Current game progression is complete.", this);
    }

    public int GetCurrentLevelNumber()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (levelSceneNames != null && levelSceneNames.Length > 0)
        {
            for (int i = 0; i < levelSceneNames.Length; i++)
            {
                if (string.Equals(levelSceneNames[i], activeScene.name, StringComparison.Ordinal))
                {
                    return i + 1;
                }
            }
        }

        int fromBuildIndex = activeScene.buildIndex - firstLevelBuildIndex + 1;
        if (fromBuildIndex <= 0)
        {
            fromBuildIndex = 1;
        }

        return Mathf.Clamp(fromBuildIndex, 1, totalLevels);
    }

    private void SetState(GameFlowState newState, bool force = false)
    {
        if (!force && currentState == newState)
        {
            return;
        }

        currentState = newState;
        isLevelThanksActive = newState == GameFlowState.Victory && ShouldShowLevelThanks();
        SetIntroStateByThis(newState == GameFlowState.Intro);
        SetGameplayBlockedByThis(newState != GameFlowState.Playing);
        ApplyTimeScaleForState(newState);
        UpdateScreensForState(newState);

        StateChanged?.Invoke(newState);
        Debug.Log($"Game state changed to {newState}", this);
    }

    private void ApplyTimeScaleForState(GameFlowState state)
    {
        switch (state)
        {
            case GameFlowState.Intro:
                Time.timeScale = 0f;
                break;
            case GameFlowState.Playing:
                Time.timeScale = 1f;
                break;
            case GameFlowState.Paused:
                Time.timeScale = freezeTimeOnPause ? 0f : 1f;
                break;
            case GameFlowState.Victory:
            case GameFlowState.Defeat:
                Time.timeScale = freezeTimeOnVictoryOrDefeat ? 0f : 1f;
                break;
        }
    }

    private void UpdateScreensForState(GameFlowState state)
    {
        SetScreenState(levelIntroScreen, state == GameFlowState.Intro && isLevelIntroActive);
        SetScreenState(pauseScreen, state == GameFlowState.Paused);
        SetScreenState(victoryScreen, state == GameFlowState.Victory && !isLevelThanksActive);
        SetScreenState(levelThanksScreen, state == GameFlowState.Victory && isLevelThanksActive);
        SetScreenState(defeatScreen, state == GameFlowState.Defeat);
    }

    private static void SetScreenState(GameObject screen, bool isVisible)
    {
        if (screen == null)
        {
            return;
        }

        if (screen.activeSelf != isVisible)
        {
            screen.SetActive(isVisible);
        }
    }

    private void ResolveFlowers()
    {
        if (trackedFlowers != null && trackedFlowers.Length > 0 && !autoFindFlowers)
        {
            return;
        }

#if UNITY_2023_1_OR_NEWER
        trackedFlowers = FindObjectsByType<FlowerEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        trackedFlowers = FindObjectsOfType<FlowerEntity>(true);
#endif
    }

    private void InitializeLevelIntro()
    {
        isLevelIntroActive = ShouldShowLevelIntro();

        if (!isLevelIntroActive)
        {
            SetScreenState(levelIntroScreen, false);
        }
    }

    private bool ShouldShowLevelIntro()
    {
        if (!showLevelIntro)
        {
            return false;
        }

        if (levelIntroScreen == null)
        {
            return false;
        }

        return true;
    }

    private bool ShouldShowLevelThanks()
    {
        if (!showLevelThanks)
        {
            return false;
        }

        if (levelThanksScreen == null)
        {
            return false;
        }

        return true;
    }

    private void SubscribeWaveSpawner()
    {
        if (waveSpawner == null)
        {
            return;
        }

        waveSpawner.AllWavesCompleted -= HandleAllWavesCompleted;
        waveSpawner.AllWavesCompleted += HandleAllWavesCompleted;
    }

    private void UnsubscribeWaveSpawner()
    {
        if (waveSpawner == null)
        {
            return;
        }

        waveSpawner.AllWavesCompleted -= HandleAllWavesCompleted;
    }

    private void SubscribeFlowers()
    {
        if (trackedFlowers == null)
        {
            return;
        }

        for (int i = 0; i < trackedFlowers.Length; i++)
        {
            FlowerEntity flower = trackedFlowers[i];
            if (flower == null)
            {
                continue;
            }

            flower.Died -= HandleFlowerDied;
            flower.Died += HandleFlowerDied;
        }
    }

    private void UnsubscribeFlowers()
    {
        if (trackedFlowers == null)
        {
            return;
        }

        for (int i = 0; i < trackedFlowers.Length; i++)
        {
            FlowerEntity flower = trackedFlowers[i];
            if (flower == null)
            {
                continue;
            }

            flower.Died -= HandleFlowerDied;
        }
    }

    private void HandleAllWavesCompleted()
    {
        if (currentState == GameFlowState.Defeat || currentState == GameFlowState.Victory)
        {
            return;
        }

        RequestEndState(GameFlowState.Victory, victoryDelaySeconds);
    }

    private void HandleFlowerDied()
    {
        if (!checkDefeatWhenAnyFlowerDies)
        {
            return;
        }

        if (currentState == GameFlowState.Defeat || currentState == GameFlowState.Victory)
        {
            return;
        }

        RequestEndState(GameFlowState.Defeat, defeatDelaySeconds);
    }

    private int CountAliveFlowers()
    {
        if (trackedFlowers == null || trackedFlowers.Length == 0)
        {
            return 0;
        }

        int aliveCount = 0;
        for (int i = 0; i < trackedFlowers.Length; i++)
        {
            FlowerEntity flower = trackedFlowers[i];
            if (flower != null && flower.IsAlive)
            {
                aliveCount++;
            }
        }

        return aliveCount;
    }

    private bool TryGetNextLevel(out string sceneName, out int buildIndex)
    {
        sceneName = null;
        buildIndex = -1;

        int currentLevelNumber = GetCurrentLevelNumber();
        if (currentLevelNumber >= totalLevels)
        {
            return false;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (levelSceneNames != null && levelSceneNames.Length > 0)
        {
            for (int i = 0; i < levelSceneNames.Length; i++)
            {
                if (!string.Equals(levelSceneNames[i], activeScene.name, StringComparison.Ordinal))
                {
                    continue;
                }

                int nextIndex = i + 1;
                if (nextIndex < levelSceneNames.Length)
                {
                    sceneName = levelSceneNames[nextIndex];
                    return true;
                }

                return false;
            }
        }

        if (!useBuildIndexProgression)
        {
            return false;
        }

        int nextBuildIndex = activeScene.buildIndex + 1;
        if (nextBuildIndex < SceneManager.sceneCountInBuildSettings)
        {
            buildIndex = nextBuildIndex;
            return true;
        }

        return false;
    }

    private bool TryGetFirstLevel(out string sceneName, out int buildIndex)
    {
        sceneName = null;
        buildIndex = -1;

        if (levelSceneNames != null && levelSceneNames.Length > 0 && !string.IsNullOrWhiteSpace(levelSceneNames[0]))
        {
            sceneName = levelSceneNames[0];
            return true;
        }

        if (firstLevelBuildIndex >= 0 && firstLevelBuildIndex < SceneManager.sceneCountInBuildSettings)
        {
            buildIndex = firstLevelBuildIndex;
            return true;
        }

        return false;
    }

    private bool IsPausePressedThisFrame()
    {
        if (Keyboard.current != null)
        {
            var keyControl = Keyboard.current[pauseKey];
            if (keyControl != null)
            {
                return keyControl.wasPressedThisFrame;
            }
        }

        return Input.GetKeyDown(KeyCode.Escape);
    }

    private void SetGameplayBlockedByThis(bool shouldBlock)
    {
        if (shouldBlock)
        {
            if (!isRegisteredGameplayBlock)
            {
                gameplayBlockersCount++;
                isRegisteredGameplayBlock = true;
            }
        }
        else if (isRegisteredGameplayBlock)
        {
            gameplayBlockersCount = Mathf.Max(0, gameplayBlockersCount - 1);
            isRegisteredGameplayBlock = false;
        }

        IsGameplayInputBlocked = gameplayBlockersCount > 0;
    }

    private void SetIntroStateByThis(bool isInIntro)
    {
        if (isInIntro)
        {
            if (!isRegisteredIntroState)
            {
                introStateCount++;
                isRegisteredIntroState = true;
            }
        }
        else if (isRegisteredIntroState)
        {
            introStateCount = Mathf.Max(0, introStateCount - 1);
            isRegisteredIntroState = false;
        }
    }

    private void RequestEndState(GameFlowState targetState, float delaySeconds)
    {
        if (targetState != GameFlowState.Victory && targetState != GameFlowState.Defeat)
        {
            return;
        }

        if (IsAnyIntroActive)
        {
            return;
        }

        if (currentState == GameFlowState.Victory || currentState == GameFlowState.Defeat)
        {
            return;
        }

        if (pendingEndStateRoutine != null)
        {
            if (pendingEndState == targetState)
            {
                return;
            }

            // Defeat has priority over victory if both triggers overlap.
            if (pendingEndState == GameFlowState.Victory && targetState == GameFlowState.Defeat)
            {
                StopCoroutine(pendingEndStateRoutine);
                pendingEndStateRoutine = null;
                pendingEndState = null;
            }
            else
            {
                return;
            }
        }

        delaySeconds = Mathf.Max(0f, delaySeconds);
        if (delaySeconds <= 0f)
        {
            SetState(targetState);
            return;
        }

        pendingEndState = targetState;
        pendingEndStateRoutine = StartCoroutine(ApplyEndStateAfterDelay(targetState, delaySeconds));
    }

    private IEnumerator ApplyEndStateAfterDelay(GameFlowState targetState, float delaySeconds)
    {
        if (useUnscaledEndStateDelay)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
        }
        else
        {
            yield return new WaitForSeconds(delaySeconds);
        }

        pendingEndStateRoutine = null;
        pendingEndState = null;

        if (currentState == GameFlowState.Victory || currentState == GameFlowState.Defeat)
        {
            yield break;
        }

        SetState(targetState);
    }

    private void ClearPendingEndState()
    {
        if (pendingEndStateRoutine != null)
        {
            StopCoroutine(pendingEndStateRoutine);
            pendingEndStateRoutine = null;
        }

        pendingEndState = null;
    }
}
