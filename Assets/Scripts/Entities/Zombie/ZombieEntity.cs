using UnityEngine;

public class ZombieEntity : LivingEntityBase
{
    [Header("Zombie")]
    [SerializeField, Min(0f)] private float attackDamage = 10f;
    [SerializeField, Min(0.1f)] private float attackCooldown = 1f;

    private float nextAttackTime;
    public bool CanAttack => IsAlive && Time.time >= nextAttackTime;

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
}
