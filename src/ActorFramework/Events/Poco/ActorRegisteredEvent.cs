namespace ActorFramework.Events.Poco;

public sealed class ActorRegisteredEvent(string actorId, string directorId)
{
    public string ActorId { get; } = actorId;
    public string DirectorId { get; } = directorId;
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}
