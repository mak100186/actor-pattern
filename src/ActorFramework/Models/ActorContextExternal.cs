namespace ActorFramework.Models;

public record ActorContextExternal(string DirectorId, string ActorId, bool IsPaused, int PendingMessagesCount, DateTimeOffset LastMessageReceivedTimestamp);
