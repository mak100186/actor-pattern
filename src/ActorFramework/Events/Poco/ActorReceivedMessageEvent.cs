namespace ActorFramework.Events.Poco;

public sealed class ActorReceivedMessageEvent
{
    public string ActorId { get; }
    public DateTimeOffset Timestamp { get; }

    public ActorReceivedMessageEvent(string actorId)
    {
        ActorId = actorId;
        Timestamp = DateTimeOffset.UtcNow;
    }
}
