namespace ActorFramework.Events;

public interface IEventListener<in TEvent>
{
    void OnEvent(TEvent evt);
}