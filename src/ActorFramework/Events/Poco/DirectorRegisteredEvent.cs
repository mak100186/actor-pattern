namespace ActorFramework.Events.Poco;

public sealed class DirectorRegisteredEvent(string directorId, string workspaceId)
{
    public string WorkspaceId { get; } = workspaceId;
    public string DirectorId { get; } = directorId;
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}
