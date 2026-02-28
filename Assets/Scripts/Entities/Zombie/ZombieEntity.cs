using UnityEngine;

public class ZombieEntity : LivingEntityBase
{
    [Header("Zombie")]
    [SerializeField, Min(0f)] private float attackDamage = 10f;
    [SerializeField, Min(0.1f)] private float attackCooldown = 1f;
    [SerializeField, Min(1f)] private float wetLightningDamageMultiplier = 1.5f;

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

    protected override void OnValidate()
    {
        base.OnValidate();
        wetVisualLerpInSpeed = Mathf.Max(0.1f, wetVisualLerpInSpeed);
        wetVisualLerpOutSpeed = Mathf.Max(0.1f, wetVisualLerpOutSpeed);
        frozenVisualLerpInSpeed = Mathf.Max(0.1f, frozenVisualLerpInSpeed);
        frozenVisualLerpOutSpeed = Mathf.Max(0.1f, frozenVisualLerpOutSpeed);
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
        wetnessRenderers = GetComponentsInChildren<Renderer>(true);
    }
}
