using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class SnowAreaEffect : MonoBehaviour
{
    [Header("Area")]
    [SerializeField, Min(0.1f)] private float radius = 2.5f;
    [SerializeField] private LayerMask zombieLayerMask = ~0;

    [Header("Slow")]
    [SerializeField, Min(0f)] private float slowDurationRefreshSeconds = 0.25f;
    [SerializeField, Min(0f)] private float movementSpeedMultiplier = 0.6f;
    [SerializeField, Min(0f)] private float animatorSpeedMultiplier = 0.7f;

    private readonly HashSet<ZombieEntity> zombiesInside = new HashSet<ZombieEntity>();
    private SphereCollider sphereCollider;

    private void Awake()
    {
        sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.radius = radius;

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnEnable()
    {
        zombiesInside.Clear();
    }

    private void Update()
    {
        if (zombiesInside.Count == 0 || slowDurationRefreshSeconds <= 0f)
        {
            return;
        }

        ZombieEntity[] zombieSnapshot = new ZombieEntity[zombiesInside.Count];
        zombiesInside.CopyTo(zombieSnapshot);
        for (int i = 0; i < zombieSnapshot.Length; i++)
        {
            ZombieEntity zombie = zombieSnapshot[i];
            if (zombie == null)
            {
                RemoveMissingZombies();
                continue;
            }

            if (!zombie.gameObject.activeInHierarchy || !zombie.IsAlive)
            {
                zombiesInside.Remove(zombie);
                continue;
            }

            zombie.ApplySnowSlow(slowDurationRefreshSeconds, movementSpeedMultiplier, animatorSpeedMultiplier);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryAddZombie(other);
    }

    private void OnTriggerExit(Collider other)
    {
        ZombieEntity zombie = other.GetComponentInParent<ZombieEntity>();
        RemoveZombie(zombie);
    }

    private void TryAddZombie(Collider other)
    {
        if (!IsLayerAllowed(other.gameObject.layer, zombieLayerMask))
        {
            return;
        }

        ZombieEntity zombie = other.GetComponentInParent<ZombieEntity>();
        if (zombie == null || !zombie.gameObject.activeInHierarchy)
        {
            return;
        }

        zombiesInside.Add(zombie);
    }

    private void RemoveZombie(ZombieEntity zombie)
    {
        if (zombie == null)
        {
            RemoveMissingZombies();
            return;
        }

        zombiesInside.Remove(zombie);
    }

    private void RemoveMissingZombies()
    {
        ZombieEntity[] snapshot = new ZombieEntity[zombiesInside.Count];
        zombiesInside.CopyTo(snapshot);

        for (int i = 0; i < snapshot.Length; i++)
        {
            ZombieEntity zombie = snapshot[i];
            if (zombie != null)
            {
                continue;
            }

            zombiesInside.Remove(zombie);
        }
    }

    private static bool IsLayerAllowed(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.1f, radius);
        movementSpeedMultiplier = Mathf.Max(0f, movementSpeedMultiplier);
        animatorSpeedMultiplier = Mathf.Max(0f, animatorSpeedMultiplier);

        if (sphereCollider != null)
        {
            sphereCollider.isTrigger = true;
            sphereCollider.radius = radius;
        }
    }
}
