using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Added to an enemy at grapple-hook time. Freezes movement via <see cref="NavMeshAgent"/>,
/// exposes <see cref="Pull"/> to set a new destination, and <see cref="Release"/> to restore
/// the agent and remove itself.
/// </summary>
public class GrappledState : MonoBehaviour
{
    private NavMeshAgent _agent;
    private EnemyBase    _enemyBase;
    private Animator     _animator;

    private static readonly int HOOKED_TRIGGER = Animator.StringToHash("Hooked");

    private void Awake()
    {
        _agent     = GetComponent<NavMeshAgent>();
        _enemyBase = GetComponent<EnemyBase>();
        _animator  = GetComponentInChildren<Animator>();

        if (_agent == null)
            Debug.LogWarning($"[GrappledState] No NavMeshAgent found on {gameObject.name}. Hook/Pull will have no effect.");
    }

    /// <summary>
    /// Freezes the enemy in place. Must be called before <see cref="Pull"/> or <see cref="Release"/>.
    /// </summary>
    public void Hook()
    {
        if (_enemyBase != null && !_enemyBase.IsAlive) return;
        if (_agent == null) return;

        _enemyBase?.SetGrappled(true);
        _agent.isStopped      = true;
        _agent.updateRotation = false;

        TrySetAnimatorTrigger(HOOKED_TRIGGER);
    }

    /// <summary>
    /// Sets <see cref="NavMeshAgent.destination"/> to <paramref name="grabPoint"/> and resumes movement.
    /// </summary>
    public void Pull(Vector3 grabPoint)
    {
        if (_agent == null) return;

        _agent.isStopped = false;
        _agent.SetDestination(grabPoint);
    }

    /// <summary>
    /// Re-enables agent movement and rotation, then destroys this component.
    /// </summary>
    public void Release()
    {
        if (_agent != null)
        {
            _agent.isStopped      = false;
            _agent.updateRotation = true;
        }

        _enemyBase?.SetGrappled(false);

        Destroy(this);
    }

    /// <summary>
    /// Ends the grapple and applies a stun of <paramref name="duration"/> seconds via <see cref="EnemyBase.Stun"/>.
    /// Destroys this component.
    /// </summary>
    public void ReleaseWithStun(float duration)
    {
        if (_agent != null)
        {
            _agent.isStopped      = false;
            _agent.updateRotation = true;
        }

        if (_enemyBase != null)
            _enemyBase.Stun(duration);
        else
            Destroy(this);

        Destroy(this);
    }

    private void TrySetAnimatorTrigger(int hash)
    {
        if (_animator == null) return;

        foreach (AnimatorControllerParameter param in _animator.parameters)
        {
            if (param.nameHash == hash)
            {
                _animator.SetTrigger(hash);
                return;
            }
        }
    }
}
