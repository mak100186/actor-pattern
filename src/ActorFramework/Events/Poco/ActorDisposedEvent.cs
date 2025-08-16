namespace ActorFramework.Events.Poco;

public sealed class ActorDisposedEvent
{
    public string ActorId { get; }
    public DateTimeOffset Timestamp { get; }

    public ActorDisposedEvent(string actorId)
    {
        ActorId = actorId;
        Timestamp = DateTimeOffset.UtcNow;
    }
}