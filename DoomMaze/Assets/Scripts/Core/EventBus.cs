using System;

/// <summary>
/// Type-safe static event bus for cross-system messaging. All communication
/// between unrelated systems must route through this class.
/// </summary>
/// <typeparam name="T">The event payload type (use structs to avoid allocations).</typeparam>
public static class EventBus<T>
{
    public static event Action<T> OnEvent;

    /// <summary>Broadcasts an event to all current subscribers.</summary>
    public static void Raise(T eventData)
    {
        OnEvent?.Invoke(eventData);
    }

    /// <summary>Subscribes a listener to this event type.</summary>
    public static void Subscribe(Action<T> listener)
    {
        OnEvent += listener;
    }

    /// <summary>Unsubscribes a listener from this event type.</summary>
    public static void Unsubscribe(Action<T> listener)
    {
        OnEvent -= listener;
    }
}
