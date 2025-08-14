using System.Threading.Channels;

using ActorFramework.Abstractions;
using ActorFramework.Runtime.Infrastructure.Internal;

using Microsoft.Extensions.Logging;

namespace ActorFramework.Runtime.Infrastructure;

/// <summary>
/// Base functionality for mailboxes that manage message queues for actors.
/// </summary>
public abstract class Mailbox<TMessage>(ILogger logger) : IMailbox<TMessage>
    where TMessage : class, IMessage
{
    protected Channel<TMessage> Channel = null!;
    protected readonly ILogger Logger = logger;

    protected long Pending;

    public int Count => (int)Interlocked.Read(ref Pending);

    public void Start() { /* no extra bootstrapping */ }

    public virtual void Stop() => Channel.Writer.Complete();

    public abstract ValueTask EnqueueAsync(TMessage message, CancellationToken cancellationToken);

    public abstract IAsyncEnumerable<MailboxTransaction<TMessage>> DequeueTransactionally(CancellationToken cancellationToken);

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