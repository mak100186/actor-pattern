using System.Threading.Channels;

using ActorFramework.Abstractions;
using ActorFramework.Configs;
using ActorFramework.Constants;
using ActorFramework.Exceptions;
using ActorFramework.Runtime.Infrastructure.Internal;

using Microsoft.Extensions.Logging;

namespace ActorFramework.Runtime.Infrastructure;

/// <summary>
/// When to use
/// - High-volume actors needing backpressure
/// - Ensure queue doesn’t grow unbounded in memory
/// - Control overflow semantics
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public sealed class BoundedMailbox<TMessage> : Mailbox<TMessage>
    where TMessage : class, IMessage
{
    /// Maximum number of messages the mailbox can hold.
    private int Capacity { get; }

    /// Defines how the mailbox handles overflow when full.
    private OverflowPolicy OverflowPolicy { get; }
    
    public BoundedMailbox(ActorFrameworkOptions actorFrameworkOptions, ILogger logger) : base(logger)
    {
        Capacity = actorFrameworkOptions?.MailboxCapacity
                   ?? ActorFrameworkConstants.DefaultMailboxCapacity;

        OverflowPolicy = actorFrameworkOptions?.MailboxOverflowPolicy
                         ?? ActorFrameworkConstants.DefaultOverflowPolicy;

        var options = new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = OverflowPolicy switch
            {
                OverflowPolicy.BlockProducer => BoundedChannelFullMode.Wait,
                OverflowPolicy.DropOldest => BoundedChannelFullMode.DropOldest,
                OverflowPolicy.DropNewest => BoundedChannelFullMode.DropNewest,
                OverflowPolicy.FailFast => BoundedChannelFullMode.DropWrite,
                _ => throw new OverflowPolicyNotHandledException(OverflowPolicy, nameof(BoundedMailbox<TMessage>))
            }
        };
        Channel = System.Threading.Channels.Channel.CreateBounded<TMessage>(options);
    }
    
    public override async ValueTask EnqueueAsync(TMessage message, CancellationToken cancellationToken)
    {
        if (Channel.Writer.TryWrite(message))
        {
            Interlocked.Increment(ref Pending);
            return;
        }

        if (OverflowPolicy == OverflowPolicy.FailFast)
        {
            throw new MailboxFullException();
        }
        
        await Channel.Writer.WriteAsync(message, cancellationToken);
        Interlocked.Increment(ref Pending);
    }

    public override async IAsyncEnumerable<MailboxTransaction<TMessage>> DequeueTransactionally(CancellationToken cancellationToken)
    {
        await foreach (var msg in Channel.Reader.ReadAllAsync(cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return new MailboxTransaction<TMessage>(
                null,
                msg,
                Logger,
                onCommit: () => Interlocked.Decrement(ref Pending),
                onRollback: () => { });
        }
    }
}
