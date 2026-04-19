using System;
using System.Collections.Generic;

/// <summary>
/// Coroutine-free timer handle returned by <see cref="TimerUtility"/>.
/// Call <see cref="Cancel"/> to stop the timer before it completes.
/// </summary>
public class TimerHandle
{
    internal Action Callback;
    internal float  Duration;
    internal bool   IsRepeating;

    public bool  IsFinished    { get; internal set; }
    public bool  IsCancelled   { get; private set; }
    public float TimeRemaining { get; internal set; }

    /// <summary>Cancels this timer so it will not fire again.</summary>
    public void Cancel() { IsCancelled = true; }
}

/// <summary>
/// Static coroutine-free timer system. Callers must call <see cref="Tick"/>
/// from their own <c>Update</c> loop to advance all active timers.
/// No heap allocations occur inside <see cref="Tick"/>.
/// </summary>
public static class TimerUtility
{
    private static readonly List<TimerHandle> _active = new List<TimerHandle>(32);

    /// <summary>Schedules a one-shot timer that fires <paramref name="onComplete"/> after <paramref name="duration"/> seconds.</summary>
    public static TimerHandle StartTimer(float duration, Action onComplete)
    {
        var handle = new TimerHandle
        {
            Callback      = onComplete,
            Duration      = duration,
            IsRepeating   = false,
            TimeRemaining = duration
        };
        _active.Add(handle);
        return handle;
    }

    /// <summary>Schedules a repeating timer that fires <paramref name="onTick"/> every <paramref name="intervalSeconds"/> seconds.</summary>
    public static TimerHandle StartRepeating(float intervalSeconds, Action onTick)
    {
        var handle = new TimerHandle
        {
            Callback      = onTick,
            Duration      = intervalSeconds,
            IsRepeating   = true,
            TimeRemaining = intervalSeconds
        };
        _active.Add(handle);
        return handle;
    }

    /// <summary>Advances all active timers by <paramref name="deltaTime"/>. Call this from a MonoBehaviour Update.</summary>
    public static void Tick(float deltaTime)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            TimerHandle handle = _active[i];

            if (handle.IsCancelled)
            {
                SwapRemove(i);
                continue;
            }

            handle.TimeRemaining -= deltaTime;

            if (handle.TimeRemaining > 0f) continue;

            handle.Callback?.Invoke();

            if (handle.IsRepeating)
            {
                handle.TimeRemaining = handle.Duration;
            }
            else
            {
                handle.IsFinished = true;
                SwapRemove(i);
            }
        }
    }

    private static void SwapRemove(int index)
    {
        int last = _active.Count - 1;
        if (index < last)
            _active[index] = _active[last];
        _active.RemoveAt(last);
    }
}
