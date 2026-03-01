using System;
using UnityEngine;

public abstract class LivingEntityBase : EntityBase, IHealth
{
    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private bool resetHealthOnEnable = true;
    [SerializeField] private bool disableObjectOnDeath = true;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0f;

    public event Action<float, float> HealthChanged;
    public event Action Died;

    protected virtual void Awake()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        CurrentHealth = maxHealth;
    }

    protected virtual void OnEnable()
    {
        if (resetHealthOnEnable)
        {
            RestoreToFull();
        }
    }

    public virtual bool TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f)
        {
            return false;
        }

        float previousValue = CurrentHealth;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        if (!Mathf.Approximately(previousValue, CurrentHealth))
        {
            OnDamaged(amount);
            RaiseHealthChanged();
        }

        if (!IsAlive)
        {
            HandleDeath();
        }

        return true;
    }

    public virtual bool Heal(float amount)
    {
        if (!IsAlive || amount <= 0f)
        {
            return false;
        }

        float previousValue = CurrentHealth;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        if (Mathf.Approximately(previousValue, CurrentHealth))
        {
            return false;
        }

        OnHealed(amount);
        RaiseHealthChanged();
        return true;
    }

    public virtual void Kill()
    {
        if (!IsAlive)
        {
            return;
        }

        CurrentHealth = 0f;
        RaiseHealthChanged();
        HandleDeath();
    }

    public virtual void SetMaxHealth(float value, bool restoreToFull = true)
    {
        maxHealth = Mathf.Max(1f, value);
        if (restoreToFull)
        {
            RestoreToFull();
            return;
        }

        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, maxHealth);
        RaiseHealthChanged();
    }

    public virtual void RestoreToFull()
    {
        CurrentHealth = maxHealth;
        RaiseHealthChanged();
    }

    protected virtual void OnDamaged(float amount)
    {
    }

    protected virtual void OnHealed(float amount)
    {
    }

    protected virtual void OnDeath()
    {
    }

    protected void NotifyDied()
    {
        OnDeath();
        Died?.Invoke();
    }

    protected void DisableObjectOnDeathIfNeeded()
    {
        if (disableObjectOnDeath)
        {
            gameObject.SetActive(false);
        }
    }

    protected virtual void HandleDeath()
    {
        NotifyDied();
        DisableObjectOnDeathIfNeeded();
    }

    protected virtual void RaiseHealthChanged()
    {
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    protected virtual void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);

        if (!Application.isPlaying)
        {
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, maxHealth);
            if (CurrentHealth <= 0f)
            {
                CurrentHealth = maxHealth;
            }
        }
    }
}
