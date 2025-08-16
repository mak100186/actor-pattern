namespace ActorFramework.Events.Poco;

public sealed class DirectorDisposedEvent
{
    public string WorkspaceId { get; }
    public string DirectorId { get; }
    public DateTimeOffset Timestamp { get; }

    public DirectorDisposedEvent(string directorId, string workspaceId)
    {
        WorkspaceId = workspaceId;
        DirectorId = directorId;
        Timestamp = DateTimeOffset.UtcNow;
    }
}
