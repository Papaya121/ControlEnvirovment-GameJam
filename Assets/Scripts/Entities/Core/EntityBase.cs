using UnityEngine;

public abstract class EntityBase : MonoBehaviour
{
    [Header("Entity")]
    [SerializeField] private string entityId = "entity";

    public string EntityId => string.IsNullOrWhiteSpace(entityId) ? name : entityId;

    protected virtual void Reset()
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            entityId = gameObject.name;
        }
    }
}
