using System.Runtime.CompilerServices;
using System.Threading.Channels;

using ActorFramework.Abstractions;

namespace ActorFramework.Runtime.Infrastructure;

/// <summary>
/// When to use
/// - Best-effort delivery
/// - Low contention workloads
/// - Simplest path for PoC or non-critical actors
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public sealed class UnboundedMailbox<TMessage> : Mailbox<TMessage>
    where TMessage : class, IMessage
{
    public UnboundedMailbox() => 
        Channel = System.Threading.Channels.Channel.CreateUnbounded<TMessage>(new()
        {
            SingleReader = true, 
            SingleWriter = false
        });
    
    public override async ValueTask EnqueueAsync(TMessage message, CancellationToken cancellationToken)
    {
        await Channel.Writer.WriteAsync(message, cancellationToken)
            .ConfigureAwait(false);
        Interlocked.Increment(ref _pending);
    }

    public override async IAsyncEnumerable<TMessage> Dequeue([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var msg in Channel.Reader.ReadAllAsync(cancellationToken)
                           .ConfigureAwait(false))
        {
            Interlocked.Decrement(ref _pending);
            yield return msg;
        }
    }
}