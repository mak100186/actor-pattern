using ActorFramework.Abstractions;

namespace ActorFramework.Events;

public interface IEventBus
{
    void Publish<TEvent>(TEvent evt);
    void Register<TEvent>(IEventListener<TEvent> listener);
    void Unregister<TEvent>(IEventListener<TEvent> listener);
}

public class InMemoryEventBus : IEventBus
{
    private readonly List<object> _listeners = new();
    private readonly object _gate = new();

    public void Register<TEvent>(IEventListener<TEvent> listener)
    {
        lock (_gate) _listeners.Add(listener);
    }

    public void Unregister<TEvent>(IEventListener<TEvent> listener)
    {
        lock (_gate) _listeners.Remove(listener);
    }

    public void Publish<TEvent>(TEvent evt)
    {
        List<IEventListener<TEvent>> snapshot;
        lock (_gate)
        {
            snapshot = _listeners.OfType<IEventListener<TEvent>>().ToList();
        }

        foreach (var listener in snapshot)
        {
            try { listener.OnEvent(evt); }
            catch { /* swallow to isolate publisher */ }
        }
    }
}
