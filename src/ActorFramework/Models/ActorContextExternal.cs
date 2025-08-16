namespace ActorFramework.Models;

public record ActorContextExternal(string DirectorId, string ActorId, int PendingMessagesCount, DateTimeOffset LastMessageReceivedTimestamp);
