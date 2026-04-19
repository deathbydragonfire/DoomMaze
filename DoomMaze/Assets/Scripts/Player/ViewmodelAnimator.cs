using UnityEngine;

/// <summary>
/// Drives the viewmodel <see cref="Animator"/> from weapon events.
/// All parameter IDs are hashed in Awake — no string lookups in frame loops.
/// </summary>
[RequireComponent(typeof(Animator))]
public class ViewmodelAnimator : MonoBehaviour
{
    private Animator _animator;

    // Pre-hashed parameter IDs
    private int _fireHash;
    private int _meleeHash;
    private int _swapHash;
    private int _isMovingHash;
    private int _speedRatioHash;

    private void Awake()
    {
        _animator = GetComponent<Animator>();

        _fireHash       = Animator.StringToHash("Fire");
        _meleeHash      = Animator.StringToHash("Melee");
        _swapHash       = Animator.StringToHash("Swap");
        _isMovingHash   = Animator.StringToHash("IsMoving");
        _speedRatioHash = Animator.StringToHash("SpeedRatio");
    }

    /// <summary>Triggers the fire animation.</summary>
    public void PlayFire()
    {
        _animator.SetTrigger(_fireHash);
    }

    /// <summary>Triggers the melee animation.</summary>
    public void PlayMelee()
    {
        _animator.SetTrigger(_meleeHash);
    }

    /// <summary>Triggers the weapon swap animation.</summary>
    public void PlaySwap()
    {
        _animator.SetTrigger(_swapHash);
    }

    /// <summary>Updates the movement blend parameters on the animator.</summary>
    public void SetMovementState(bool isMoving, float speedRatio)
    {
        _animator.SetBool(_isMovingHash, isMoving);
        _animator.SetFloat(_speedRatioHash, speedRatio);
    }
}
