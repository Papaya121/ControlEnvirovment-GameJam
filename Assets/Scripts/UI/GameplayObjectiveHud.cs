using System.Text;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class GameplayObjectiveHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private ZombieWaveSpawner waveSpawner;

    [Header("Text")]
    [SerializeField] private bool showControlsHint = true;
    [SerializeField] private string controlsHint = "1 Rain (hold LMB) | 2 Lightning (click) | 3 Snow (hold LMB)";

    private readonly StringBuilder builder = new StringBuilder(256);

    private void Awake()
    {
        if (objectiveText == null)
        {
            objectiveText = GetComponent<TMP_Text>();
        }

        if (gameStateManager == null)
        {
            gameStateManager = FindFirstObjectByType<GameStateManager>();
        }

        if (waveSpawner == null)
        {
            waveSpawner = FindFirstObjectByType<ZombieWaveSpawner>();
        }
    }

    private void OnEnable()
    {
        if (gameStateManager != null)
        {
            gameStateManager.StateChanged -= HandleGameStateChanged;
            gameStateManager.StateChanged += HandleGameStateChanged;
        }

        RefreshText();
    }

    private void OnDisable()
    {
        if (gameStateManager != null)
        {
            gameStateManager.StateChanged -= HandleGameStateChanged;
        }
    }

    private void Update()
    {
        RefreshText();
    }

    private void HandleGameStateChanged(GameStateManager.GameFlowState _)
    {
        RefreshText();
    }

    private void RefreshText()
    {
        if (objectiveText == null)
        {
            return;
        }

        builder.Clear();
        builder.AppendLine("Goal: Survive all waves and keep at least one flower alive.");

        if (gameStateManager != null)
        {
            builder.Append("State: ");
            builder.Append(gameStateManager.CurrentState);
            builder.AppendLine();

            builder.Append("Flowers: ");
            builder.Append(gameStateManager.AliveFlowerCount);
            builder.Append("/");
            builder.Append(gameStateManager.TotalFlowerCount);
            builder.AppendLine();
        }

        if (waveSpawner != null && waveSpawner.TotalWaveCount > 0)
        {
            int currentWave = waveSpawner.CurrentWaveNumber;
            int totalWaves = waveSpawner.TotalWaveCount;
            int completedWaves = waveSpawner.CompletedWaveCount;

            if (currentWave > 0)
            {
                builder.Append("Wave: ");
                builder.Append(currentWave);
                builder.Append("/");
                builder.Append(totalWaves);
            }
            else
            {
                builder.Append("Waves cleared: ");
                builder.Append(completedWaves);
                builder.Append("/");
                builder.Append(totalWaves);
            }

            builder.AppendLine();
        }

        if (showControlsHint && !string.IsNullOrWhiteSpace(controlsHint))
        {
            builder.Append(controlsHint);
        }

        objectiveText.text = builder.ToString();
    }
}
