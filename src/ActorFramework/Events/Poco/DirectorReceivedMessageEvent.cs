namespace ActorFramework.Events.Poco;

public sealed class DirectorReceivedMessageEvent(string directorId)
{
    public string DirectorId { get; } = directorId;
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}
