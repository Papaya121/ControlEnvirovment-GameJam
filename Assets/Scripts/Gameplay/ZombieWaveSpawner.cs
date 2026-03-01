using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class ZombieWaveSpawner : MonoBehaviour
{
    [System.Serializable]
    private class ZombieWave
    {
        [Min(1)] public int zombieCount = 8;
        [Min(0f)] public float delayBeforeWave = 2f;
        [Min(0f)] public float spawnInterval = 0.6f;
        [Min(0)] public int maxAliveAtOnce = 0;
        public bool waitUntilWaveCleared = true;
        [Range(0f, 100f)] public float runnerSpawnChancePercent = 25f;
    }

    [Header("References")]
    [SerializeField] private ZombiePool zombiePool;
    [SerializeField] private Transform[] spawnPoints;

    [Header("Spawn Position")]
    [SerializeField, Min(0f)] private float randomRadius = 4f;
    [SerializeField, Min(0.01f)] private float navMeshSampleDistance = 5f;
    [SerializeField, Min(1)] private int positionSearchAttempts = 10;

    [Header("Wave Flow")]
    [SerializeField, Min(0f)] private float startDelay = 1f;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool loopWaves = true;
    [SerializeField] private ZombieWave[] waves = { new ZombieWave() };

    [Header("Wave Bars")]
    [SerializeField] private Bar[] waveBars;
    [SerializeField] private bool hideUnusedWaveBars = true;

    [Header("Victory")]
    [SerializeField] private bool stopTimeOnVictory = true;
    [SerializeField] private bool disableSpawnerOnVictory = true;

    private Coroutine spawnRoutine;
    private int[] waveSpawnedCounts;
    private int[] waveDefeatedCounts;
    private bool[] waveCompletedFlags;
    private readonly Dictionary<ZombieEntity, int> zombieWaveMap = new Dictionary<ZombieEntity, int>();
    private readonly Dictionary<ZombieEntity, System.Action> zombieDeathHandlers = new Dictionary<ZombieEntity, System.Action>();
    private int currentWaveIndex = -1;
    private int completedWaveCount;

    public bool IsRunning => spawnRoutine != null;
    public int TotalWaveCount => waves != null ? waves.Length : 0;
    public int CurrentWaveNumber => currentWaveIndex >= 0 ? currentWaveIndex + 1 : 0;
    public int CompletedWaveCount => completedWaveCount;
    public event System.Action AllWavesCompleted;

    private void OnEnable()
    {
        if (playOnEnable)
        {
            StartSpawning();
        }
    }

    private void OnDisable()
    {
        StopSpawning();
    }

    public void StartSpawning()
    {
        if (spawnRoutine != null)
        {
            return;
        }

        if (!CanSpawn())
        {
            Debug.LogWarning("ZombieWaveSpawner is not configured correctly.", this);
            return;
        }

        PrepareWaveTracking();
        ResetWaveBars(true);
        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        if (spawnRoutine == null)
        {
            return;
        }

        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
        ClearZombieTracking();
    }

    private IEnumerator SpawnLoop()
    {
        while (GameStateManager.IsAnyIntroActive)
        {
            yield return null;
        }

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        while (true)
        {
            PrepareWaveTracking();
            ResetWaveBars(false);

            for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
            {
                while (GameStateManager.IsAnyIntroActive)
                {
                    yield return null;
                }

                ZombieWave wave = waves[waveIndex];
                if (wave == null || wave.zombieCount <= 0)
                {
                    MarkWaveCompleted(waveIndex, true);
                    continue;
                }

                BeginWave(waveIndex);

                if (wave.delayBeforeWave > 0f)
                {
                    yield return new WaitForSeconds(wave.delayBeforeWave);
                }

                int spawned = 0;
                while (spawned < wave.zombieCount)
                {
                    while (GameStateManager.IsAnyIntroActive)
                    {
                        yield return null;
                    }

                    if (wave.maxAliveAtOnce > 0)
                    {
                        while (zombiePool.ActiveCount >= wave.maxAliveAtOnce)
                        {
                            yield return null;
                        }
                    }

                    if (TrySpawnZombie(out ZombieEntity spawnedZombie))
                    {
                        float runnerChance01 = Mathf.Clamp01(wave.runnerSpawnChancePercent * 0.01f);
                        bool isRunner = Random.value < runnerChance01;
                        spawnedZombie.ConfigureRunner(isRunner);
                        spawned++;
                        RegisterZombieForWave(spawnedZombie, waveIndex);
                    }

                    if (wave.spawnInterval > 0f)
                    {
                        yield return new WaitForSeconds(wave.spawnInterval);
                    }
                    else
                    {
                        yield return null;
                    }
                }

                if (wave.waitUntilWaveCleared)
                {
                    while (zombiePool.ActiveCount > 0)
                    {
                        yield return null;
                    }
                }

                MarkWaveCompleted(waveIndex, true);
            }

            if (!loopWaves)
            {
                HandleAllWavesCompleted();
                break;
            }
        }

        spawnRoutine = null;
    }

    private bool TrySpawnZombie(out ZombieEntity zombie)
    {
        zombie = null;

        if (!TryGetSpawnCenter(out Vector3 spawnCenter))
        {
            return false;
        }

        Vector3 spawnPosition = ResolveSpawnPosition(spawnCenter);
        Quaternion spawnRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        zombie = zombiePool.Spawn(spawnPosition, spawnRotation);
        return zombie != null;
    }

    private Vector3 ResolveSpawnPosition(Vector3 center)
    {
        int attempts = Mathf.Max(1, positionSearchAttempts);
        for (int i = 0; i < attempts; i++)
        {
            Vector2 offset = Random.insideUnitCircle * randomRadius;
            Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);
            if (TrySampleNavMesh(candidate, out Vector3 sampled))
            {
                return sampled;
            }
        }

        if (TrySampleNavMesh(center, out Vector3 centerSampled))
        {
            return centerSampled;
        }

        return center;
    }

    private bool TrySampleNavMesh(Vector3 position, out Vector3 result)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }

        result = position;
        return false;
    }

    private bool CanSpawn()
    {
        return zombiePool != null
            && waves != null
            && waves.Length > 0;
    }

    private bool TryGetSpawnCenter(out Vector3 center)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                Transform candidate = spawnPoints[Random.Range(0, spawnPoints.Length)];
                if (candidate != null)
                {
                    center = candidate.position;
                    return true;
                }
            }
        }

        center = transform.position;
        return true;
    }

    private void Reset()
    {
        zombiePool = GetComponent<ZombiePool>();
    }

    private void PrepareWaveTracking()
    {
        int waveCount = waves != null ? waves.Length : 0;
        if (waveSpawnedCounts == null || waveSpawnedCounts.Length != waveCount)
        {
            waveSpawnedCounts = new int[waveCount];
            waveDefeatedCounts = new int[waveCount];
            waveCompletedFlags = new bool[waveCount];
        }
        else
        {
            System.Array.Clear(waveSpawnedCounts, 0, waveSpawnedCounts.Length);
            System.Array.Clear(waveDefeatedCounts, 0, waveDefeatedCounts.Length);
            System.Array.Clear(waveCompletedFlags, 0, waveCompletedFlags.Length);
        }

        currentWaveIndex = -1;
        completedWaveCount = 0;
        ConfigureWaveBarsVisibility();
        ClearZombieTracking();
    }

    private void ConfigureWaveBarsVisibility()
    {
        if (waveBars == null)
        {
            return;
        }

        int waveCount = waves != null ? waves.Length : 0;
        int revealedWaveCount = hideUnusedWaveBars
            ? Mathf.Clamp(completedWaveCount + 1, 0, waveCount)
            : waveCount;

        for (int i = 0; i < waveBars.Length; i++)
        {
            Bar bar = waveBars[i];
            if (bar == null)
            {
                continue;
            }

            bool shouldBeVisible = !hideUnusedWaveBars || i < revealedWaveCount;
            if (bar.gameObject.activeSelf != shouldBeVisible)
            {
                bar.gameObject.SetActive(shouldBeVisible);
            }
        }
    }

    private void ResetWaveBars(bool instant)
    {
        int waveCount = waves != null ? waves.Length : 0;
        for (int i = 0; i < waveCount; i++)
        {
            SetWaveBarValue(i, 0f, instant);
        }
    }

    private void BeginWave(int waveIndex)
    {
        if (!IsWaveIndexValid(waveIndex))
        {
            return;
        }

        currentWaveIndex = waveIndex;
        waveSpawnedCounts[waveIndex] = 0;
        waveDefeatedCounts[waveIndex] = 0;
        waveCompletedFlags[waveIndex] = false;
        SetWaveBarValue(waveIndex, 0f, false);
    }

    private void RegisterZombieForWave(ZombieEntity zombie, int waveIndex)
    {
        if (zombie == null || !IsWaveIndexValid(waveIndex))
        {
            return;
        }

        UnregisterZombie(zombie);
        zombieWaveMap[zombie] = waveIndex;

        System.Action deathHandler = null;
        deathHandler = () => HandleTrackedZombieDeath(zombie);
        zombieDeathHandlers[zombie] = deathHandler;
        zombie.Died += deathHandler;

        waveSpawnedCounts[waveIndex]++;
    }

    private void HandleTrackedZombieDeath(ZombieEntity zombie)
    {
        if (zombie == null)
        {
            return;
        }

        if (zombieWaveMap.TryGetValue(zombie, out int waveIndex) && IsWaveIndexValid(waveIndex))
        {
            waveDefeatedCounts[waveIndex] = Mathf.Min(waveDefeatedCounts[waveIndex] + 1, GetWaveZombieCount(waveIndex));
            if (!waveCompletedFlags[waveIndex])
            {
                UpdateWaveProgress(waveIndex);
            }
        }

        UnregisterZombie(zombie);
    }

    private void UpdateWaveProgress(int waveIndex)
    {
        if (!IsWaveIndexValid(waveIndex))
        {
            return;
        }

        int total = GetWaveZombieCount(waveIndex);
        if (total <= 0)
        {
            SetWaveBarValue(waveIndex, 1f, false);
            return;
        }

        float progress = Mathf.Clamp01((float)waveDefeatedCounts[waveIndex] / total);
        SetWaveBarValue(waveIndex, progress, false);
    }

    private void MarkWaveCompleted(int waveIndex, bool instant)
    {
        if (!IsWaveIndexValid(waveIndex))
        {
            return;
        }

        if (!waveCompletedFlags[waveIndex])
        {
            completedWaveCount = Mathf.Min(completedWaveCount + 1, TotalWaveCount);
        }

        waveCompletedFlags[waveIndex] = true;
        SetWaveBarValue(waveIndex, 1f, instant);
        ConfigureWaveBarsVisibility();
    }

    private void SetWaveBarValue(int waveIndex, float normalized, bool instant)
    {
        if (waveBars == null || waveIndex < 0 || waveIndex >= waveBars.Length)
        {
            return;
        }

        Bar bar = waveBars[waveIndex];
        if (bar == null)
        {
            return;
        }

        bar.SetNormalized(normalized, instant);
    }

    private bool IsWaveIndexValid(int waveIndex)
    {
        return waveSpawnedCounts != null
            && waveIndex >= 0
            && waveIndex < waveSpawnedCounts.Length;
    }

    private int GetWaveZombieCount(int waveIndex)
    {
        if (waves == null || waveIndex < 0 || waveIndex >= waves.Length)
        {
            return 0;
        }

        ZombieWave wave = waves[waveIndex];
        return wave != null ? Mathf.Max(0, wave.zombieCount) : 0;
    }

    private void UnregisterZombie(ZombieEntity zombie)
    {
        if (zombie == null)
        {
            return;
        }

        if (zombieDeathHandlers.TryGetValue(zombie, out System.Action handler))
        {
            zombie.Died -= handler;
            zombieDeathHandlers.Remove(zombie);
        }

        zombieWaveMap.Remove(zombie);
    }

    private void ClearZombieTracking()
    {
        if (zombieDeathHandlers.Count > 0)
        {
            List<ZombieEntity> zombies = new List<ZombieEntity>(zombieDeathHandlers.Keys);
            for (int i = 0; i < zombies.Count; i++)
            {
                ZombieEntity zombie = zombies[i];
                if (zombie == null)
                {
                    continue;
                }

                if (zombieDeathHandlers.TryGetValue(zombie, out System.Action handler))
                {
                    zombie.Died -= handler;
                }
            }
        }

        zombieWaveMap.Clear();
        zombieDeathHandlers.Clear();
    }

    private void HandleAllWavesCompleted()
    {
        currentWaveIndex = -1;
        for (int i = 0; i < (waves != null ? waves.Length : 0); i++)
        {
            MarkWaveCompleted(i, false);
        }

        bool hasExternalVictoryListener = AllWavesCompleted != null;
        AllWavesCompleted?.Invoke();
        Debug.Log("All waves completed. Victory.", this);

        if (hasExternalVictoryListener)
        {
            if (disableSpawnerOnVictory)
            {
                enabled = false;
            }

            return;
        }

        if (stopTimeOnVictory)
        {
            Time.timeScale = 0f;
        }

        if (disableSpawnerOnVictory)
        {
            enabled = false;
        }
    }
}
