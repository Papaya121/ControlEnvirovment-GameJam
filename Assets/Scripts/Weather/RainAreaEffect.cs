using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class RainAreaEffect : MonoBehaviour
{
    [Header("Area")]
    [SerializeField, Min(0.1f)] private float radius = 2.5f;
    [SerializeField] private LayerMask flowerLayerMask = ~0;
    [SerializeField] private LayerMask zombieLayerMask = ~0;

    [Header("Healing")]
    [SerializeField, Min(0f)] private float healPerSecond = 2f;
    [SerializeField, Min(0f)] private float wetDurationRefreshSeconds = 0.25f;

    private readonly HashSet<FlowerEntity> flowersInside = new HashSet<FlowerEntity>();
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
        flowersInside.Clear();
        zombiesInside.Clear();
    }

    private void OnDisable()
    {
        ReleaseAllSuppression();
    }

    private void Update()
    {
        if (flowersInside.Count == 0 && zombiesInside.Count == 0)
        {
            return;
        }

        float healAmount = Mathf.Max(0f, healPerSecond * Time.deltaTime);

        // Iterate over a copy because entries can get invalid while objects deactivate.
        FlowerEntity[] snapshot = new FlowerEntity[flowersInside.Count];
        flowersInside.CopyTo(snapshot);

        for (int i = 0; i < snapshot.Length; i++)
        {
            FlowerEntity flower = snapshot[i];
            if (flower == null)
            {
                RemoveMissingFlowers();
                continue;
            }

            if (!flower.gameObject.activeInHierarchy)
            {
                RemoveFlower(flower);
                continue;
            }

            if (!flower.IsAlive)
            {
                continue;
            }

            if (wetDurationRefreshSeconds > 0f)
            {
                flower.ApplyWet(wetDurationRefreshSeconds);
            }

            if (healAmount > 0f)
            {
                flower.ApplyWater(healAmount);
            }
        }

        if (zombiesInside.Count == 0 || wetDurationRefreshSeconds <= 0f)
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

            zombie.ApplyWet(wetDurationRefreshSeconds);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryAddFlower(other);
        TryAddZombie(other);
    }

    private void OnTriggerExit(Collider other)
    {
        FlowerEntity flower = other.GetComponentInParent<FlowerEntity>();
        RemoveFlower(flower);

        ZombieEntity zombie = other.GetComponentInParent<ZombieEntity>();
        RemoveZombie(zombie);
    }

    private void TryAddFlower(Collider other)
    {
        if (!IsLayerAllowed(other.gameObject.layer))
        {
            return;
        }

        FlowerEntity flower = other.GetComponentInParent<FlowerEntity>();
        if (flower == null || !flower.gameObject.activeInHierarchy)
        {
            return;
        }

        if (!flowersInside.Add(flower))
        {
            return;
        }

        flower.SetDryOutSuppressed(true);
    }

    private void RemoveFlower(FlowerEntity flower)
    {
        if (flower == null)
        {
            RemoveMissingFlowers();
            return;
        }

        if (!flowersInside.Remove(flower))
        {
            return;
        }

        flower.SetDryOutSuppressed(false);
    }

    private void RemoveMissingFlowers()
    {
        FlowerEntity[] snapshot = new FlowerEntity[flowersInside.Count];
        flowersInside.CopyTo(snapshot);

        for (int i = 0; i < snapshot.Length; i++)
        {
            FlowerEntity flower = snapshot[i];
            if (flower != null)
            {
                continue;
            }

            flowersInside.Remove(flower);
        }
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

    private bool IsLayerAllowed(int layer)
    {
        return IsLayerAllowed(layer, flowerLayerMask);
    }

    private static bool IsLayerAllowed(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void ReleaseAllSuppression()
    {
        if (flowersInside.Count == 0)
        {
            zombiesInside.Clear();
            return;
        }

        FlowerEntity[] snapshot = new FlowerEntity[flowersInside.Count];
        flowersInside.CopyTo(snapshot);
        flowersInside.Clear();

        for (int i = 0; i < snapshot.Length; i++)
        {
            FlowerEntity flower = snapshot[i];
            if (flower == null)
            {
                continue;
            }

            flower.SetDryOutSuppressed(false);
        }

        zombiesInside.Clear();
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.1f, radius);

        if (sphereCollider != null)
        {
            sphereCollider.isTrigger = true;
            sphereCollider.radius = radius;
        }
    }
}
