using UnityEngine;

public class FlowerEntity : LivingEntityBase
{
    [Header("Flower")]
    [SerializeField, Min(0f)] private float dryOutDamagePerSecond = 1f;
    
    private int dryOutSuppressionSources;

    private void Update()
    {
        if (!IsAlive || dryOutDamagePerSecond <= 0f || IsDryOutSuppressed)
        {
            return;
        }

        TakeDamage(dryOutDamagePerSecond * Time.deltaTime);
    }

    public bool ApplyWater(float waterPower)
    {
        return Heal(waterPower);
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
}
