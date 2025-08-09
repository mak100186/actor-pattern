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
public sealed class UnboundedMailbox<TMessage> : Mailbox<TMessage>, IMailbox<TMessage> 
    where TMessage : class, IMessage
{
    public UnboundedMailbox() => 
        Channel = System.Threading.Channels.Channel.CreateUnbounded<TMessage>(new()
        {
            SingleReader = true, 
            SingleWriter = false
        });
    private long _pending;

    public int Count => (int)Interlocked.Read(ref _pending);

    public void Start() { /* nothing to bootstrap */ }

    public void Stop() => Channel.Writer.Complete();

    public async ValueTask EnqueueAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        await Channel.Writer.WriteAsync(message, cancellationToken)
            .ConfigureAwait(false);
        Interlocked.Increment(ref _pending);
    }

    public async IAsyncEnumerable<TMessage> Dequeue([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var msg in Channel.Reader.ReadAllAsync(cancellationToken)
                           .ConfigureAwait(false))
        {
            Interlocked.Decrement(ref _pending);
            yield return msg;
        }
    }

    public void Dispose() => Stop();
}