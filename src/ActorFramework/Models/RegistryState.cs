namespace ActorFramework.Models;

public record RegistryState(bool IsPaused, int PendingMessageCount, string TimestampText, string ExceptionText);