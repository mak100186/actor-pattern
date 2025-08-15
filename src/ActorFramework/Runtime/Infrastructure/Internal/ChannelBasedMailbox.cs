using System.Threading.Channels;

using ActorFramework.Abstractions;

using Microsoft.Extensions.Logging;

namespace ActorFramework.Runtime.Infrastructure.Internal;

public abstract class ChannelBasedMailbox<TMessage>(ILogger logger) : Mailbox<TMessage>(logger)
    where TMessage : class, IMessage
{
    protected Channel<TMessage> Channel = null!;

    public override void Stop() => Channel.Writer.Complete();
}