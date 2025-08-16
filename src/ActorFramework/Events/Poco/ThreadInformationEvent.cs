namespace ActorFramework.Events.Poco;

public sealed class ThreadInformationEvent
{
    public string ActorId { get; }
    public string ThreadId { get; }
    public string DirectorId { get; }
    public DateTimeOffset Timestamp { get; }

    public ThreadInformationEvent(string directorId, string actorId, string threadId)
    {
        ActorId = actorId;
        ThreadId = threadId;
        DirectorId = directorId;
        Timestamp = DateTimeOffset.UtcNow;
    }
}