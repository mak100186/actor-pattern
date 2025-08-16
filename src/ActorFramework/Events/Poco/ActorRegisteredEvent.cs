namespace ActorFramework.Events.Poco;

public sealed class ActorRegisteredEvent
{
    public string ActorId { get; }
    public string DirectorId { get; }
    public DateTimeOffset Timestamp { get; }

    public ActorRegisteredEvent(string actorId, string directorId)
    {
        ActorId = actorId;
        DirectorId = directorId;
        Timestamp = DateTimeOffset.UtcNow;
    }
}
