using System.Threading.Channels;

using ActorFramework.Abstractions;

namespace ActorFramework.Runtime.Infrastructure;

/// <summary>
/// Base functionality for mailboxes that manage message queues for actors.
/// </summary>
public abstract class Mailbox<TMessage> 
    where TMessage : class, IMessage
{
    protected Channel<TMessage> Channel = null!;
}