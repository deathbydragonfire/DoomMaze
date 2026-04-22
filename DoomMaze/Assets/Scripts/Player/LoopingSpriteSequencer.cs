using System.Collections;
using UnityEngine;

/// <summary>
/// Extends <see cref="PunchSpriteSequencer"/> with a three-phase fire animation:
/// intro (played once on fire start) → loop (repeated while held) → outro (played once on release).
/// Used by <see cref="MachineGunSpriteSequencer"/> and <see cref="FlamethrowerSpriteSequencer"/>.
/// </summary>
public class LoopingSpriteSequencer : PunchSpriteSequencer
{
    [SerializeField] private PunchAnimationSet _introSet;
    [SerializeField] private PunchAnimationSet _loopSet;
    [SerializeField] private PunchAnimationSet _outroSet;

    private bool      _isFiringLoop;
    private Coroutine _loopCoroutine;

    /// <summary>
    /// Begins the intro-then-loop sequence. Re-entrant calls while already looping are ignored.
    /// </summary>
    public override void StartFiring()
    {
        if (_isFiringLoop) return;

        _isFiringLoop = true;

        if (_loopCoroutine != null)
            StopCoroutine(_loopCoroutine);

        _loopCoroutine = StartCoroutine(FireSequenceCoroutine());
    }

    /// <summary>
    /// Signals the loop to stop. The coroutine will finish the current loop iteration,
    /// play the outro set once, then return to idle.
    /// </summary>
    public override void StopFiring()
    {
        _isFiringLoop = false;
    }

    /// <summary>
    /// No-op — animation is driven entirely by <see cref="StartFiring"/> and <see cref="StopFiring"/>.
    /// </summary>
    public override void PlayNextPunch() { }

    private IEnumerator FireSequenceCoroutine()
    {
        if (_introSet != null)
            yield return StartCoroutine(PlaySet(_introSet));

        while (_isFiringLoop)
        {
            if (_loopSet != null)
                yield return StartCoroutine(PlaySet(_loopSet));
            else
                yield return null;
        }

        if (_outroSet != null)
            yield return StartCoroutine(PlaySet(_outroSet));

        ShowIdle();
        _loopCoroutine = null;
    }
}
