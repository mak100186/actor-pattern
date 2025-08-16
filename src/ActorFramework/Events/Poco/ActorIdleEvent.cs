namespace ActorFramework.Events.Poco;

public sealed class ActorIdleEvent(string actorId)
{
    public string ActorId { get; } = actorId;
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}
