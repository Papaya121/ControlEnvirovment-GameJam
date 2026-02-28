using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LightningAreaEffect : MonoBehaviour
{
    [Header("Area")]
    [SerializeField, Min(0.1f)] private float radius = 2.5f;
    [SerializeField] private LayerMask zombieLayerMask = ~0;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float damage = 30f;
    [SerializeField, Min(0f)] private float damageSpread = 5f;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Lifetime")]
    [SerializeField, Min(0f)] private float destroyAfterSeconds = 1.5f;

    private readonly HashSet<ZombieEntity> damagedZombies = new HashSet<ZombieEntity>();

    private void OnEnable()
    {
        ApplyDamageOnce();
        ScheduleDestroy();
    }

    public void ApplyDamageOnce()
    {
        damagedZombies.Clear();
        float strikeDamage = ResolveStrikeDamage();

        Collider[] hits = Physics.OverlapSphere(transform.position, radius, zombieLayerMask, triggerInteraction);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            ZombieEntity zombie = hit.GetComponentInParent<ZombieEntity>();
            if (zombie == null || !zombie.IsAlive)
            {
                continue;
            }

            if (!damagedZombies.Add(zombie))
            {
                continue;
            }

            zombie.TakeDamage(strikeDamage);
        }
    }

    private void ScheduleDestroy()
    {
        if (destroyAfterSeconds <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        Destroy(gameObject, destroyAfterSeconds);
    }

    private float ResolveStrikeDamage()
    {
        if (damageSpread <= 0f)
        {
            return damage;
        }

        float min = Mathf.Max(0f, damage - damageSpread);
        float max = damage + damageSpread;
        return Random.Range(min, max);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.95f, 1f, 0.3f);
        Gizmos.DrawSphere(transform.position, radius);
        Gizmos.color = new Color(0.9f, 0.95f, 1f, 1f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.1f, radius);
        damage = Mathf.Max(0f, damage);
        damageSpread = Mathf.Max(0f, damageSpread);
        destroyAfterSeconds = Mathf.Max(0f, destroyAfterSeconds);
    }
}
