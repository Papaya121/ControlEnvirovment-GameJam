using UnityEngine;
using UnityEngine.AI;

public class ZombieEntity : LivingEntityBase
{
    [Header("Zombie")]
    [SerializeField, Min(0f)] private float attackDamage = 10f;
    [SerializeField, Min(0.1f)] private float attackCooldown = 1f;
    [SerializeField, Min(1f)] private float wetLightningDamageMultiplier = 1.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource voiceAudioSource;
    [SerializeField] private AudioClip[] randomVoiceClips;
    [SerializeField, Min(0.1f)] private float minVoiceIntervalSeconds = 2f;
    [SerializeField, Min(0.1f)] private float maxVoiceIntervalSeconds = 6f;
    [SerializeField] private AudioSource deathAudioSource;
    [SerializeField] private AudioClip deathClip;

    [Header("Death Sequence")]
    [SerializeField] private ZombieAnimatorController animatorController;
    [SerializeField] private ZombieNavMeshController navMeshController;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField, Min(0f)] private float timeBeforeSinkSeconds = 2f;
    [SerializeField, Min(0.01f)] private float sinkSpeed = 0.9f;
    [SerializeField, Min(0f)] private float sinkDistance = 1.8f;
    [SerializeField, Min(0f)] private float hideDelayFromSinkStartSeconds = 2f;

    [Header("Wet Visuals")]
    [SerializeField] private Renderer[] wetnessRenderers;
    [SerializeField] private string wetnessShaderProperty = "_Wetness";
    [SerializeField] private string frozenShaderProperty = "_Frozen";
    [SerializeField, Min(0.1f)] private float wetVisualLerpInSpeed = 8f;
    [SerializeField, Min(0.1f)] private float wetVisualLerpOutSpeed = 3f;
    [SerializeField, Min(0.1f)] private float frozenVisualLerpInSpeed = 6f;
    [SerializeField, Min(0.1f)] private float frozenVisualLerpOutSpeed = 4f;

    private float nextAttackTime;
    private float wetUntilTime;
    private float snowSlowUntilTime;
    private float snowMoveSpeedMultiplier = 1f;
    private float snowAnimatorSpeedMultiplier = 1f;
    private float currentWetnessVisual;
    private float currentFrozenVisual;
    private MaterialPropertyBlock wetnessPropertyBlock;
    private int wetnessPropertyId;
    private int frozenPropertyId;
    private float nextVoiceTime;
    private bool deathSequenceStarted;
    private bool visualsHidden;
    private Coroutine deathSequenceRoutine;
    public bool CanAttack => IsAlive && Time.time >= nextAttackTime;
    public bool IsWet => IsAlive && Time.time < wetUntilTime;
    public bool IsSlowedBySnow => IsAlive && Time.time < snowSlowUntilTime;
    public float CurrentMovementSpeedMultiplier => IsSlowedBySnow ? snowMoveSpeedMultiplier : 1f;
    public float CurrentAnimatorSpeedMultiplier => IsSlowedBySnow ? snowAnimatorSpeedMultiplier : 1f;

    protected override void Awake()
    {
        base.Awake();

        if (wetnessRenderers == null || wetnessRenderers.Length == 0)
        {
            wetnessRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (string.IsNullOrWhiteSpace(wetnessShaderProperty))
        {
            wetnessShaderProperty = "_Wetness";
        }
        
        if (string.IsNullOrWhiteSpace(frozenShaderProperty))
        {
            frozenShaderProperty = "_Frozen";
        }

        wetnessPropertyBlock = new MaterialPropertyBlock();
        wetnessPropertyId = Shader.PropertyToID(wetnessShaderProperty);
        frozenPropertyId = Shader.PropertyToID(frozenShaderProperty);

        if (voiceAudioSource == null)
        {
            voiceAudioSource = GetComponent<AudioSource>();
        }

        if (deathAudioSource == null)
        {
            deathAudioSource = voiceAudioSource;
        }

        if (animatorController == null)
        {
            animatorController = GetComponentInChildren<ZombieAnimatorController>(true);
        }

        if (navMeshController == null)
        {
            navMeshController = GetComponent<ZombieNavMeshController>();
            if (navMeshController == null)
            {
                navMeshController = GetComponentInParent<ZombieNavMeshController>();
            }
        }

        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
            if (navMeshAgent == null)
            {
                navMeshAgent = GetComponentInParent<NavMeshAgent>();
            }
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        wetUntilTime = 0f;
        snowSlowUntilTime = 0f;
        snowMoveSpeedMultiplier = 1f;
        snowAnimatorSpeedMultiplier = 1f;
        currentWetnessVisual = 0f;
        currentFrozenVisual = 0f;
        ApplyWeatherVisuals(currentWetnessVisual, currentFrozenVisual);
        ScheduleNextVoice();
        deathSequenceStarted = false;
        deathSequenceRoutine = null;
        visualsHidden = false;
        SetRenderersVisible(true);

        if (navMeshController != null)
        {
            navMeshController.enabled = true;
        }

        if (navMeshAgent != null && !navMeshAgent.enabled)
        {
            navMeshAgent.enabled = true;
        }
    }

    private void Update()
    {
        float targetWetnessVisual = IsWet ? 1f : 0f;
        float wetLerpSpeed = targetWetnessVisual > currentWetnessVisual ? wetVisualLerpInSpeed : wetVisualLerpOutSpeed;
        currentWetnessVisual = Mathf.MoveTowards(currentWetnessVisual, targetWetnessVisual, wetLerpSpeed * Time.deltaTime);

        float targetFrozenVisual = IsSlowedBySnow ? 1f : 0f;
        float frozenLerpSpeed = targetFrozenVisual > currentFrozenVisual ? frozenVisualLerpInSpeed : frozenVisualLerpOutSpeed;
        currentFrozenVisual = Mathf.MoveTowards(currentFrozenVisual, targetFrozenVisual, frozenLerpSpeed * Time.deltaTime);

        ApplyWeatherVisuals(currentWetnessVisual, currentFrozenVisual);
        TryPlayRandomVoice();
    }

    public bool TryStartAttack()
    {
        if (!CanAttack)
        {
            return false;
        }

        nextAttackTime = Time.time + attackCooldown;
        return true;
    }

    public bool ApplyAttack(IHealth target)
    {
        if (!IsAlive || target == null || !target.IsAlive)
        {
            return false;
        }

        return target.TakeDamage(attackDamage);
    }

    public bool TryAttack(IHealth target)
    {
        if (!TryStartAttack())
        {
            return false;
        }

        return ApplyAttack(target);
    }

    public void ApplyWet(float durationSeconds)
    {
        if (!IsAlive || durationSeconds <= 0f)
        {
            return;
        }

        wetUntilTime = Mathf.Max(wetUntilTime, Time.time + durationSeconds);
    }

    public float ResolveLightningDamage(float baseDamage)
    {
        if (baseDamage <= 0f)
        {
            return 0f;
        }

        return IsWet ? baseDamage * wetLightningDamageMultiplier : baseDamage;
    }

    public void ApplySnowSlow(float durationSeconds, float movementMultiplier, float animatorMultiplier)
    {
        if (!IsAlive || durationSeconds <= 0f)
        {
            return;
        }

        snowSlowUntilTime = Mathf.Max(snowSlowUntilTime, Time.time + durationSeconds);
        snowMoveSpeedMultiplier = Mathf.Clamp01(movementMultiplier);
        snowAnimatorSpeedMultiplier = Mathf.Clamp01(animatorMultiplier);
    }

    private void ApplyWeatherVisuals(float wetnessValue, float frozenValue)
    {
        if (wetnessRenderers == null || wetnessPropertyBlock == null)
        {
            return;
        }

        for (int i = 0; i < wetnessRenderers.Length; i++)
        {
            Renderer rendererTarget = wetnessRenderers[i];
            if (rendererTarget == null)
            {
                continue;
            }

            rendererTarget.GetPropertyBlock(wetnessPropertyBlock);
            wetnessPropertyBlock.SetFloat(wetnessPropertyId, wetnessValue);
            wetnessPropertyBlock.SetFloat(frozenPropertyId, frozenValue);
            rendererTarget.SetPropertyBlock(wetnessPropertyBlock);
        }
    }

    private void TryPlayRandomVoice()
    {
        if (!IsAlive || voiceAudioSource == null || randomVoiceClips == null || randomVoiceClips.Length == 0)
        {
            return;
        }

        if (Time.time < nextVoiceTime)
        {
            return;
        }

        if (voiceAudioSource.isPlaying)
        {
            return;
        }

        AudioClip selectedClip = null;
        int startIndex = Random.Range(0, randomVoiceClips.Length);
        for (int i = 0; i < randomVoiceClips.Length; i++)
        {
            int index = (startIndex + i) % randomVoiceClips.Length;
            AudioClip candidate = randomVoiceClips[index];
            if (candidate == null)
            {
                continue;
            }

            selectedClip = candidate;
            break;
        }

        if (selectedClip == null)
        {
            return;
        }

        voiceAudioSource.PlayOneShot(selectedClip);
        ScheduleNextVoice();
    }

    private void ScheduleNextVoice()
    {
        float minInterval = Mathf.Max(0.1f, minVoiceIntervalSeconds);
        float maxInterval = Mathf.Max(minInterval, maxVoiceIntervalSeconds);
        nextVoiceTime = Time.time + Random.Range(minInterval, maxInterval);
    }

    protected override void HandleDeath()
    {
        if (deathSequenceStarted)
        {
            return;
        }

        deathSequenceStarted = true;
        deathSequenceRoutine = StartCoroutine(DeathSequenceRoutine());
    }

    private System.Collections.IEnumerator DeathSequenceRoutine()
    {
        if (voiceAudioSource != null && voiceAudioSource.isPlaying)
        {
            voiceAudioSource.Stop();
        }

        if (deathAudioSource != null && deathClip != null)
        {
            deathAudioSource.PlayOneShot(deathClip);
        }

        animatorController?.PlayDeath();

        if (timeBeforeSinkSeconds > 0f)
        {
            yield return new WaitForSeconds(timeBeforeSinkSeconds);
        }

        if (navMeshController != null)
        {
            navMeshController.enabled = false;
        }

        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            navMeshAgent.enabled = false;
        }

        if (sinkDistance > 0f)
        {
            Vector3 startPosition = transform.position;
            float targetY = startPosition.y - sinkDistance;
            float sinkStartedAt = Time.time;
            while (transform.position.y > targetY)
            {
                if (!visualsHidden && Time.time >= sinkStartedAt + hideDelayFromSinkStartSeconds)
                {
                    SetRenderersVisible(false);
                    visualsHidden = true;
                }

                transform.position += Vector3.down * sinkSpeed * Time.deltaTime;
                yield return null;
            }
        }

        deathSequenceRoutine = null;
        base.HandleDeath();
    }

    private void OnDisable()
    {
        if (deathSequenceRoutine != null)
        {
            StopCoroutine(deathSequenceRoutine);
            deathSequenceRoutine = null;
        }

        if (navMeshController != null)
        {
            navMeshController.enabled = true;
        }

        if (navMeshAgent != null && !navMeshAgent.enabled)
        {
            navMeshAgent.enabled = true;
        }
    }

    private void SetRenderersVisible(bool isVisible)
    {
        if (wetnessRenderers == null)
        {
            return;
        }

        for (int i = 0; i < wetnessRenderers.Length; i++)
        {
            Renderer rendererTarget = wetnessRenderers[i];
            if (rendererTarget == null)
            {
                continue;
            }

            rendererTarget.enabled = isVisible;
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        wetVisualLerpInSpeed = Mathf.Max(0.1f, wetVisualLerpInSpeed);
        wetVisualLerpOutSpeed = Mathf.Max(0.1f, wetVisualLerpOutSpeed);
        frozenVisualLerpInSpeed = Mathf.Max(0.1f, frozenVisualLerpInSpeed);
        frozenVisualLerpOutSpeed = Mathf.Max(0.1f, frozenVisualLerpOutSpeed);
        minVoiceIntervalSeconds = Mathf.Max(0.1f, minVoiceIntervalSeconds);
        maxVoiceIntervalSeconds = Mathf.Max(minVoiceIntervalSeconds, maxVoiceIntervalSeconds);
        timeBeforeSinkSeconds = Mathf.Max(0f, timeBeforeSinkSeconds);
        sinkSpeed = Mathf.Max(0.01f, sinkSpeed);
        sinkDistance = Mathf.Max(0f, sinkDistance);
        hideDelayFromSinkStartSeconds = Mathf.Max(0f, hideDelayFromSinkStartSeconds);
        if (string.IsNullOrWhiteSpace(wetnessShaderProperty))
        {
            wetnessShaderProperty = "_Wetness";
        }
        if (string.IsNullOrWhiteSpace(frozenShaderProperty))
        {
            frozenShaderProperty = "_Frozen";
        }
    }

    private void Reset()
    {
        voiceAudioSource = GetComponent<AudioSource>();
        deathAudioSource = voiceAudioSource;
        animatorController = GetComponentInChildren<ZombieAnimatorController>(true);
        navMeshController = GetComponent<ZombieNavMeshController>();
        if (navMeshController == null)
        {
            navMeshController = GetComponentInParent<ZombieNavMeshController>();
        }
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponentInParent<NavMeshAgent>();
        }
        wetnessRenderers = GetComponentsInChildren<Renderer>(true);
    }
}
