using UnityEngine;
using System.Collections.Generic;

public class FlowerEntity : LivingEntityBase
{
    [Header("Flower")]
    [SerializeField, Min(0f)] private float dryOutDamagePerSecond = 1f;
    [SerializeField, Min(0f)] private float dryLightningDamage = 20f;
    [SerializeField, Min(0f)] private float wetLightningHeal = 16f;
    
    private int dryOutSuppressionSources;
    private float wetUntilTime;
    private static readonly List<FlowerEntity> ActiveFlowersInternal = new List<FlowerEntity>();

    private void Update()
    {
        if (!IsAlive || dryOutDamagePerSecond <= 0f || IsDryOutSuppressed)
        {
            return;
        }

        TakeDamage(dryOutDamagePerSecond * Time.deltaTime);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (!ActiveFlowersInternal.Contains(this))
        {
            ActiveFlowersInternal.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveFlowersInternal.Remove(this);
        wetUntilTime = 0f;
    }

    public bool ApplyWater(float waterPower)
    {
        return Heal(waterPower);
    }

    public void ApplyWet(float durationSeconds)
    {
        if (!IsAlive || durationSeconds <= 0f)
        {
            return;
        }

        wetUntilTime = Mathf.Max(wetUntilTime, Time.time + durationSeconds);
    }

    public void ApplyLightningImpact()
    {
        if (!IsAlive)
        {
            return;
        }

        if (IsWet)
        {
            Heal(wetLightningHeal);
            return;
        }

        TakeDamage(dryLightningDamage);
    }

    public void SetDryOutSuppressed(bool isSuppressed)
    {
        if (isSuppressed)
        {
            dryOutSuppressionSources++;
            return;
        }

        dryOutSuppressionSources = Mathf.Max(0, dryOutSuppressionSources - 1);
    }

    public bool IsDryOutSuppressed => dryOutSuppressionSources > 0;
    public bool IsWet => IsAlive && Time.time < wetUntilTime;
    public static IReadOnlyList<FlowerEntity> ActiveFlowers => ActiveFlowersInternal;
}
