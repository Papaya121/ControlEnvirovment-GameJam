using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class ZombieAnimatorController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private ZombieEntity zombie;
    [SerializeField] private ZombieNavMeshController navMeshController;

    [Header("Animator Params")]
    [SerializeField] private string velocityParameter = "Velocity";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string deathTrigger = "Death";
    [SerializeField, Min(0f)] private float idleVelocityValue = 1f;
    [SerializeField, Min(0f)] private float walkVelocityValue = 1f;
    [SerializeField, Min(0f)] private float runVelocityValue = 2f;

    [Header("Tuning")]
    [SerializeField, Min(0.01f)] private float runAtWorldSpeed = 3.5f;
    [SerializeField, Min(0f)] private float moveThreshold = 0.05f;
    [SerializeField, Min(0f)] private float velocitySmoothing = 12f;

    private int velocityParamHash;
    private int attackTriggerHash;
    private int deathTriggerHash;
    private float currentVelocityValue;

    private void Awake()
    {
        if (zombie == null)
        {
            zombie = GetComponent<ZombieEntity>();
            if (zombie == null)
            {
                zombie = GetComponentInParent<ZombieEntity>();
            }
        }

        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
            if (navMeshAgent == null)
            {
                navMeshAgent = GetComponentInParent<NavMeshAgent>();
            }
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }
        }

        if (navMeshController == null)
        {
            navMeshController = GetComponent<ZombieNavMeshController>();
            if (navMeshController == null)
            {
                navMeshController = GetComponentInParent<ZombieNavMeshController>();
            }
        }

        velocityParamHash = Animator.StringToHash(velocityParameter);
        attackTriggerHash = Animator.StringToHash(attackTrigger);
        deathTriggerHash = Animator.StringToHash(deathTrigger);
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

    private void Update()
    {
        if (animator == null || (zombie != null && !zombie.IsAlive))
        {
            return;
        }

        float targetVelocityValue = ResolveVelocityValue();
        float lerpFactor = 1f - Mathf.Exp(-velocitySmoothing * Time.deltaTime);
        currentVelocityValue = Mathf.Lerp(currentVelocityValue, targetVelocityValue, lerpFactor);
        animator.SetFloat(velocityParamHash, currentVelocityValue);
    }

    public void PlayAttack()
    {
        if (animator == null || (zombie != null && !zombie.IsAlive))
        {
            return;
        }

        animator.SetTrigger(attackTriggerHash);
    }

    // Called from Animation Event with exact function name: AttackEvent
    public void AttackEvent()
    {
        navMeshController?.ApplyAttackFromAnimationEvent();
    }

    private void HandleDeath()
    {
        if (animator == null)
        {
            return;
        }

        animator.SetTrigger(deathTriggerHash);
    }

    private float ResolveVelocityValue()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return idleVelocityValue;
        }

        float currentSpeed = navMeshAgent.velocity.magnitude;
        if (currentSpeed <= moveThreshold)
        {
            return idleVelocityValue;
        }

        float runBlend = Mathf.Clamp01(currentSpeed / runAtWorldSpeed);
        return Mathf.Lerp(walkVelocityValue, runVelocityValue, runBlend);
    }

    private void Reset()
    {
        zombie = GetComponent<ZombieEntity>();
        if (zombie == null)
        {
            zombie = GetComponentInParent<ZombieEntity>();
        }

        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponentInParent<NavMeshAgent>();
        }

        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        navMeshController = GetComponent<ZombieNavMeshController>();
        if (navMeshController == null)
        {
            navMeshController = GetComponentInParent<ZombieNavMeshController>();
        }
    }
}
