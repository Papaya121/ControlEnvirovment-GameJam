using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(ZombieEntity))]
public class ZombieNavMeshController : MonoBehaviour
{
    [Header("Search")]
    [SerializeField, Min(0.1f)] private float targetSearchInterval = 0.5f;
    [SerializeField, Min(0.05f)] private float repathInterval = 0.25f;

    [Header("Combat")]
    [SerializeField, Min(0.1f)] private float attackDistance = 1.8f;

    [Header("Speed")]
    [SerializeField] private bool useRunSpeedWhenFar = true;
    [SerializeField, Min(0.1f)] private float walkSpeed = 1.8f;
    [SerializeField, Min(0.1f)] private float runSpeed = 3.6f;
    [SerializeField, Min(0f)] private float runDistance = 8f;

    [Header("References")]
    [SerializeField] private ZombieEntity zombie;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private ZombieAnimatorController animatorController;

    private FlowerEntity currentTarget;
    private FlowerEntity attackTarget;
    private float nextTargetSearchTime;
    private float nextRepathTime;

    private void Awake()
    {
        if (zombie == null)
        {
            zombie = GetComponent<ZombieEntity>();
        }

        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (animatorController == null)
        {
            animatorController = GetComponentInChildren<ZombieAnimatorController>(true);
        }
    }

    private void OnEnable()
    {
        nextTargetSearchTime = 0f;
        nextRepathTime = 0f;
    }

    private void Update()
    {
        if (zombie == null || !zombie.IsAlive)
        {
            StopMovement();
            return;
        }

        if (ShouldRetarget())
        {
            FindNearestFlower();
            nextTargetSearchTime = Time.time + targetSearchInterval;
        }

        if (currentTarget == null)
        {
            StopMovement();
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
        UpdateMovementSpeed(distanceToTarget);

        if (distanceToTarget <= attackDistance)
        {
            StopMovement();

            if (animatorController == null)
            {
                zombie.TryAttack(currentTarget);
                return;
            }

            if (!zombie.TryStartAttack())
            {
                return;
            }

            attackTarget = currentTarget;
            animatorController.PlayAttack();
            return;
        }

        MoveToCurrentTarget();
    }

    private bool ShouldRetarget()
    {
        return currentTarget == null || !currentTarget.IsAlive || Time.time >= nextTargetSearchTime;
    }

    private void MoveToCurrentTarget()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh || currentTarget == null)
        {
            return;
        }

        navMeshAgent.isStopped = false;
        if (Time.time < nextRepathTime)
        {
            return;
        }

        nextRepathTime = Time.time + repathInterval;
        navMeshAgent.SetDestination(currentTarget.transform.position);
    }

    private void StopMovement()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return;
        }

        navMeshAgent.isStopped = true;
        navMeshAgent.ResetPath();
    }

    private void UpdateMovementSpeed(float distanceToTarget)
    {
        if (navMeshAgent == null)
        {
            return;
        }

        float snowMultiplier = zombie != null ? zombie.CurrentMovementSpeedMultiplier : 1f;

        if (!useRunSpeedWhenFar)
        {
            navMeshAgent.speed = walkSpeed * snowMultiplier;
            return;
        }

        float baseSpeed = distanceToTarget > runDistance ? runSpeed : walkSpeed;
        navMeshAgent.speed = baseSpeed * snowMultiplier;
    }

    private void FindNearestFlower()
    {
        IReadOnlyList<FlowerEntity> flowers = FlowerEntity.ActiveFlowers;
        currentTarget = null;
        float bestDistanceSqr = float.MaxValue;
        Vector3 ownPosition = transform.position;

        for (int i = 0; i < flowers.Count; i++)
        {
            FlowerEntity candidate = flowers[i];
            if (candidate == null || !candidate.IsAlive)
            {
                continue;
            }

            float distanceSqr = (candidate.transform.position - ownPosition).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            currentTarget = candidate;
        }
    }

    private void Reset()
    {
        zombie = GetComponent<ZombieEntity>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        animatorController = GetComponentInChildren<ZombieAnimatorController>(true);
    }

    public void ApplyAttackFromAnimationEvent()
    {
        if (zombie == null || !zombie.IsAlive)
        {
            attackTarget = null;
            return;
        }

        FlowerEntity target = attackTarget;
        attackTarget = null;

        if (target == null || !target.IsAlive)
        {
            target = currentTarget;
        }

        if (target == null || !target.IsAlive)
        {
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
        if (distanceToTarget > attackDistance + 0.5f)
        {
            return;
        }

        zombie.ApplyAttack(target);
    }
}
