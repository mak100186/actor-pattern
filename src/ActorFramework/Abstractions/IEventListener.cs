namespace ActorFramework.Abstractions;

public interface IEventListener<in TEvent>
{
    void OnEvent(TEvent evt);
}