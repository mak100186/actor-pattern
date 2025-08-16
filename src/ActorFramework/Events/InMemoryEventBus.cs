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

    public void Register<TEvent>(IEventListener<TEvent> listener)
    {
        _listeners.Add(listener);
    }

    public void Unregister<TEvent>(IEventListener<TEvent> listener)
    {
        _listeners.Remove(listener);
    }

    public void Publish<TEvent>(TEvent evt)
    {
        foreach (IEventListener<TEvent> listener in _listeners.OfType<IEventListener<TEvent>>())
        {
            listener.OnEvent(evt);
        }
    }
}