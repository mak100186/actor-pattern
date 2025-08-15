using ActorFramework.Abstractions;

namespace ActorFramework.Models;

/// <summary>
/// The state of the mailbox for the actor. This is an external representation of the state.
/// </summary>
/// <param name="PendingMessageCount">The count of messages still pending in the mailbox for this actor</param>
/// <param name="Messages">Serialized messages in the mailbox</param>
public record MailboxStateExternal(int PendingMessageCount, IMessage[] Messages);