namespace ActorFramework.Events.Poco;

public sealed class WorkspaceCapacityReachedEvent(string workspaceId)
{
    public string WorkspaceId { get; } = workspaceId;
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}
