using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ZombieEntity))]
public class ZombiePoolMember : MonoBehaviour
{
    [SerializeField] private ZombieEntity zombie;

    public ZombieEntity Zombie => zombie;
    public ZombiePool Owner { get; private set; }
    public bool IsInPool { get; private set; }

    private void Awake()
    {
        if (zombie == null)
        {
            zombie = GetComponent<ZombieEntity>();
        }
    }

    private void OnEnable()
    {
        if (zombie != null)
        {
            zombie.Died += HandleDeath;
        }
    }

    private void OnDisable()
    {
        if (zombie != null)
        {
            zombie.Died -= HandleDeath;
        }
    }

    internal void Initialize(ZombiePool owner, ZombieEntity entity)
    {
        Owner = owner;
        zombie = entity != null ? entity : GetComponent<ZombieEntity>();
        IsInPool = false;
    }

    internal void MarkSpawned()
    {
        IsInPool = false;
    }

    internal void MarkReturned()
    {
        IsInPool = true;
    }

    private void HandleDeath()
    {
        Owner?.Release(this);
    }

    private void Reset()
    {
        zombie = GetComponent<ZombieEntity>();
    }
}
