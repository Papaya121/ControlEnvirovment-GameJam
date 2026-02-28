using System;

public interface IHealth
{
    float MaxHealth { get; }
    float CurrentHealth { get; }
    bool IsAlive { get; }

    event Action<float, float> HealthChanged;
    event Action Died;

    bool TakeDamage(float amount);
    bool Heal(float amount);
    void Kill();
    void SetMaxHealth(float value, bool restoreToFull = true);
}
