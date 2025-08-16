namespace ActorFramework.Events.Poco;

public sealed class WorkspaceCapacityReachedEvent
{
    public string WorkspaceId { get; }
    public DateTimeOffset Timestamp { get; }

    public WorkspaceCapacityReachedEvent(string workspaceId)
    {
        WorkspaceId = workspaceId;
        Timestamp = DateTimeOffset.UtcNow;
    }
}
