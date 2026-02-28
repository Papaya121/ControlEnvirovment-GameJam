using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class GameStateManager : MonoBehaviour
{
    public enum GameFlowState
    {
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
    [SerializeField] private bool checkDefeatWhenAllFlowersDead = true;
    [SerializeField, Min(0f)] private float victoryDelaySeconds = 1f;
    [SerializeField, Min(0f)] private float defeatDelaySeconds = 1f;
    [SerializeField] private bool useUnscaledEndStateDelay = true;

    [Header("UI Screens")]
    [SerializeField] private GameObject pauseScreen;
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private GameObject defeatScreen;

    [Header("Levels")]
    [SerializeField, Min(1)] private int totalLevels = 5;
    [SerializeField] private string[] levelSceneNames;
    [SerializeField] private bool useBuildIndexProgression = true;
    [SerializeField, Min(0)] private int firstLevelBuildIndex = 0;

    private GameFlowState currentState = GameFlowState.Playing;
    private Coroutine pendingEndStateRoutine;
    private GameFlowState? pendingEndState;

    public static bool IsGameplayInputBlocked { get; private set; }

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
        SetState(GameFlowState.Playing, true);
    }

    private void OnEnable()
    {
        SubscribeWaveSpawner();
        SubscribeFlowers();

        if (autoStartWaves && waveSpawner != null && !waveSpawner.IsRunning)
        {
            waveSpawner.StartSpawning();
        }
    }

    private void OnDisable()
    {
        UnsubscribeWaveSpawner();
        UnsubscribeFlowers();
        ClearPendingEndState();

        if (currentState != GameFlowState.Victory && currentState != GameFlowState.Defeat)
        {
            Time.timeScale = 1f;
            IsGameplayInputBlocked = false;
        }
    }

    private void Update()
    {
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
        if (currentState == GameFlowState.Playing)
        {
            SetState(GameFlowState.Paused);
        }
    }

    public void ResumeGame()
    {
        if (currentState == GameFlowState.Paused)
        {
            SetState(GameFlowState.Playing);
        }
    }

    public void RestartCurrentLevel()
    {
        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
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
        ApplyTimeScaleForState(newState);
        UpdateScreensForState(newState);
        IsGameplayInputBlocked = newState != GameFlowState.Playing;

        StateChanged?.Invoke(newState);
        Debug.Log($"Game state changed to {newState}", this);
    }

    private void ApplyTimeScaleForState(GameFlowState state)
    {
        switch (state)
        {
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
        SetScreenState(pauseScreen, state == GameFlowState.Paused);
        SetScreenState(victoryScreen, state == GameFlowState.Victory);
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
        if (!checkDefeatWhenAllFlowersDead)
        {
            return;
        }

        if (currentState == GameFlowState.Defeat || currentState == GameFlowState.Victory)
        {
            return;
        }

        if (!AreAnyFlowersAlive())
        {
            RequestEndState(GameFlowState.Defeat, defeatDelaySeconds);
        }
    }

    private bool AreAnyFlowersAlive()
    {
        return CountAliveFlowers() > 0;
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

    private void RequestEndState(GameFlowState targetState, float delaySeconds)
    {
        if (targetState != GameFlowState.Victory && targetState != GameFlowState.Defeat)
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
