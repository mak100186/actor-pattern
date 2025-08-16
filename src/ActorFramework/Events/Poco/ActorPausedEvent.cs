namespace ActorFramework.Events.Poco;

public sealed class ActorPausedEvent
{
    public string ActorId { get; }
    public Exception? Exception { get; }
    public DateTimeOffset Timestamp { get; }

    public ActorPausedEvent(string actorId, Exception? exception)
    {
        ActorId = actorId;
        Exception = exception;
        Timestamp = DateTimeOffset.UtcNow;
    }
}
