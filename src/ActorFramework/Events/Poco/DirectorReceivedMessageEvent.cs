namespace ActorFramework.Events.Poco;

public sealed class DirectorReceivedMessageEvent
{
    public string DirectorId { get; }
    public DateTimeOffset Timestamp { get; }

    public DirectorReceivedMessageEvent(string directorId)
    {
        DirectorId = directorId;
        Timestamp = DateTimeOffset.UtcNow;
    }
}
