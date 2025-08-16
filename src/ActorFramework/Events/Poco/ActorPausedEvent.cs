namespace ActorFramework.Events.Poco;

public sealed class ActorPausedEvent(string actorId, Exception? exception)
{
    public string ActorId { get; } = actorId;
    public Exception? Exception { get; } = exception;
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}
