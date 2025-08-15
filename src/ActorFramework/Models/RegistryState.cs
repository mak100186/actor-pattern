namespace ActorFramework.Models;

/// <summary>
/// The state of the registry for actors. This is an external representation of the actor registry state.
/// </summary>
/// <param name="IsPaused">Whether the actor is in a paused state due to an error/exception.</param>
/// <param name="PendingMessageCount">The count of messages still pending in the mailbox for this actor</param>
/// <param name="TimestampText">The timestamp of the last message processed by this actor</param>
/// <param name="ExceptionText">The exception details of the last exception received by this actor. This gets overriden if the last message was processed successfully</param>
public record RegistryState(bool IsPaused, int PendingMessageCount, string TimestampText, string ExceptionText);