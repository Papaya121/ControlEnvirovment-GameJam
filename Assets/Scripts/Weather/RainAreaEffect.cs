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

    [Header("Healing")]
    [SerializeField, Min(0f)] private float healPerSecond = 2f;

    private readonly HashSet<FlowerEntity> flowersInside = new HashSet<FlowerEntity>();
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
    }

    private void OnDisable()
    {
        ReleaseAllSuppression();
    }

    private void Update()
    {
        if (flowersInside.Count == 0 || healPerSecond <= 0f)
        {
            return;
        }

        float healAmount = healPerSecond * Time.deltaTime;
        if (healAmount <= 0f)
        {
            return;
        }

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

            flower.ApplyWater(healAmount);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryAddFlower(other);
    }

    private void OnTriggerExit(Collider other)
    {
        FlowerEntity flower = other.GetComponentInParent<FlowerEntity>();
        RemoveFlower(flower);
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

    private bool IsLayerAllowed(int layer)
    {
        return (flowerLayerMask.value & (1 << layer)) != 0;
    }

    private void ReleaseAllSuppression()
    {
        if (flowersInside.Count == 0)
        {
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
