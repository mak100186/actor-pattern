namespace ActorFramework.Events.Poco;

public sealed class ActorResumedEvent
{
    public string ActorId { get; }
    public DateTimeOffset Timestamp { get; }

    public ActorResumedEvent(string actorId)
    {
        ActorId = actorId;
        Timestamp = DateTimeOffset.UtcNow;
    }
}
