using System.Threading.Channels;

using ActorFramework.Abstractions;

namespace ActorFramework.Runtime.Infrastructure;

/// <summary>
/// Base functionality for mailboxes that manage message queues for actors.
/// </summary>
public abstract class Mailbox<TMessage> : IMailbox<TMessage>
    where TMessage : class, IMessage
{
    protected Channel<TMessage> Channel = null!;

    protected long _pending;

    public int Count => (int)Interlocked.Read(ref _pending);

    public void Start() { /* no extra bootstrapping */ }

    public void Stop() => Channel.Writer.Complete();

    public abstract ValueTask EnqueueAsync(TMessage message, CancellationToken cancellationToken);

    public abstract IAsyncEnumerable<TMessage> Dequeue(CancellationToken cancellationToken);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Stop();
        }
    }
}