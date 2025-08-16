namespace ActorFramework.Events.Poco;

public sealed class ThreadInformationEvent(string directorId, string actorId, string threadId)
{
    public string ActorId { get; } = actorId;
    public string ThreadId { get; } = threadId;
    public string DirectorId { get; } = directorId;
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}