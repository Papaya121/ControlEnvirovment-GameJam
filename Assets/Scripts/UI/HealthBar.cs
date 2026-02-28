using UnityEngine;

[DisallowMultipleComponent]
public class HealthBar : Bar
{
    [Header("Health Target")]
    [SerializeField] private LivingEntityBase target;
    [SerializeField] private bool hideIfNoTarget = false;
    [SerializeField] private bool hideWhenDead = false;

    protected override void Awake()
    {
        base.Awake();

        if (target == null)
        {
            target = GetComponentInParent<LivingEntityBase>();
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Subscribe();
        Refresh();
    }

    protected override void OnDisable()
    {
        Unsubscribe();
        base.OnDisable();
    }

    public void SetTarget(LivingEntityBase newTarget)
    {
        if (target == newTarget)
        {
            return;
        }

        Unsubscribe();
        target = newTarget;
        Subscribe();
        Refresh();
    }

    private void Subscribe()
    {
        if (target == null)
        {
            return;
        }

        target.HealthChanged += HandleHealthChanged;
        target.Died += HandleDeath;
    }

    private void Unsubscribe()
    {
        if (target == null)
        {
            return;
        }

        target.HealthChanged -= HandleHealthChanged;
        target.Died -= HandleDeath;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        SetValue(currentHealth, maxHealth);
    }

    private void HandleDeath()
    {
        if (!hideWhenDead)
        {
            return;
        }

        gameObject.SetActive(false);
    }

    private void Refresh()
    {
        if (target == null)
        {
            if (hideIfNoTarget)
            {
                gameObject.SetActive(false);
            }

            return;
        }

        SetValue(target.CurrentHealth, target.MaxHealth, true);
    }

    protected override void Reset()
    {
        base.Reset();
        target = GetComponentInParent<LivingEntityBase>();
    }
}
