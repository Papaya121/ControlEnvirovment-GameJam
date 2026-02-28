using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ZombiePool : MonoBehaviour
{
    [Header("Pool")]
    [SerializeField] private ZombieEntity zombiePrefab;
    [SerializeField, Min(0)] private int initialSize = 12;
    [SerializeField] private bool canExpand = true;
    [SerializeField] private Transform container;

    private readonly Queue<ZombiePoolMember> available = new Queue<ZombiePoolMember>();
    private readonly HashSet<ZombiePoolMember> availableLookup = new HashSet<ZombiePoolMember>();
    private readonly HashSet<ZombiePoolMember> allMembers = new HashSet<ZombiePoolMember>();

    public int TotalCount => allMembers.Count;
    public int AvailableCount => available.Count;
    public int ActiveCount => TotalCount - AvailableCount;

    private void Awake()
    {
        if (container == null)
        {
            container = transform;
        }

        Prewarm();
    }

    public ZombieEntity Spawn(Vector3 position, Quaternion rotation)
    {
        ZombiePoolMember member = GetAvailableOrCreate();
        if (member == null)
        {
            return null;
        }

        Transform memberTransform = member.transform;
        memberTransform.SetParent(container);
        memberTransform.SetPositionAndRotation(position, rotation);

        member.MarkSpawned();
        member.gameObject.SetActive(true);
        return member.Zombie;
    }

    public void Release(ZombieEntity zombie)
    {
        if (zombie == null)
        {
            return;
        }

        ZombiePoolMember member = zombie.GetComponent<ZombiePoolMember>();
        Release(member);
    }

    internal void Release(ZombiePoolMember member)
    {
        if (member == null || member.Owner != this || member.IsInPool)
        {
            return;
        }

        member.MarkReturned();
        member.transform.SetParent(container);

        if (member.gameObject.activeSelf)
        {
            member.gameObject.SetActive(false);
        }

        if (availableLookup.Add(member))
        {
            available.Enqueue(member);
        }
    }

    private void Prewarm()
    {
        if (zombiePrefab == null)
        {
            Debug.LogWarning("ZombiePool has no zombie prefab assigned.", this);
            return;
        }

        int count = Mathf.Max(0, initialSize);
        for (int i = 0; i < count; i++)
        {
            CreateMember();
        }
    }

    private ZombiePoolMember GetAvailableOrCreate()
    {
        while (available.Count > 0)
        {
            ZombiePoolMember member = available.Dequeue();
            availableLookup.Remove(member);
            if (member != null)
            {
                return member;
            }
        }

        if (!canExpand)
        {
            return null;
        }

        return CreateMember();
    }

    private ZombiePoolMember CreateMember()
    {
        if (zombiePrefab == null)
        {
            return null;
        }

        ZombieEntity zombie = Instantiate(zombiePrefab, container);
        ZombiePoolMember member = zombie.GetComponent<ZombiePoolMember>();
        if (member == null)
        {
            member = zombie.gameObject.AddComponent<ZombiePoolMember>();
        }

        member.Initialize(this, zombie);
        allMembers.Add(member);
        Release(member);
        return member;
    }

    private void Reset()
    {
        container = transform;
    }
}
