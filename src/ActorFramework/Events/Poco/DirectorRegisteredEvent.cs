namespace ActorFramework.Events.Poco;

public sealed class DirectorRegisteredEvent
{
    public string WorkspaceId { get; }
    public string DirectorId { get; }
    public DateTimeOffset Timestamp { get; }

    public DirectorRegisteredEvent(string directorId, string workspaceId)
    {
        WorkspaceId = workspaceId;
        DirectorId = directorId;
        Timestamp = DateTimeOffset.UtcNow;
    }
}
