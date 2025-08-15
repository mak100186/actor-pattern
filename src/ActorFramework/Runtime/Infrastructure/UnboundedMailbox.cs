using System.Threading.Channels;

using ActorFramework.Abstractions;
using ActorFramework.Runtime.Infrastructure.Internal;

using Microsoft.Extensions.Logging;

namespace ActorFramework.Runtime.Infrastructure;

/// <summary>
/// When to use
/// - Best-effort delivery
/// - Low contention workloads
/// - Simplest path for PoC or non-critical actors
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public sealed class UnboundedMailbox<TMessage> : ChannelBasedMailbox<TMessage>
    where TMessage : class, IMessage
{
    public UnboundedMailbox(ILogger logger): base(logger) => 
        Channel = System.Threading.Channels.Channel.CreateUnbounded<TMessage>(new()
        {
            SingleReader = true, 
            SingleWriter = false
        });
    
    public override async ValueTask EnqueueAsync(TMessage message, CancellationToken cancellationToken)
    {
        await Channel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref Pending);
    }

    public override async IAsyncEnumerable<MailboxTransaction<TMessage>> DequeueAsync(CancellationToken cancellationToken)
    {
        await foreach (var msg in Channel.Reader.ReadAllAsync(cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return new(null, msg, OnCommitInternal, OnRollbackInternal);
        }
    }
}