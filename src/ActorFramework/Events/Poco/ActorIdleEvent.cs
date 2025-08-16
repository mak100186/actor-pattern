namespace ActorFramework.Events.Poco;

public sealed class ActorIdleEvent
{
    public string ActorId { get; }
    public DateTimeOffset Timestamp { get; }

    public ActorIdleEvent(string actorId)
    {
        ActorId = actorId;
        Timestamp = DateTimeOffset.UtcNow;
    }
}
