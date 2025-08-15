using ActorFramework.Abstractions;

namespace ActorFramework.Models;

/// <summary>
/// The state of the registry for actors. This is an external representation of the actor registry state.
/// </summary>
/// <param name="IsPaused">Whether the actor is in a paused state due to an error/exception.</param>
/// <param name="MailboxState">The count of messages still pending in the mailbox for this actor</param>
/// <param name="TimestampText">The timestamp of the last message processed by this actor</param>
/// <param name="ExceptionText">The exception details of the last exception received by this actor. This gets overriden if the last message was processed successfully</param>
public record RegistryState<TMessage>(bool IsPaused, MailboxState<TMessage> MailboxState, string TimestampText, string ExceptionText)
    where TMessage : class, IMessage;

/// <summary>
/// The state of the mailbox for the actor. This is an external representation of the state.
/// </summary>
/// <param name="PendingMessageCount">The count of messages still pending in the mailbox for this actor</param>
/// <param name="Messages">Serialized messages in the mailbox</param>
public record MailboxState<TMessage>(int PendingMessageCount, TMessage[] Messages)
    where TMessage : class, IMessage;