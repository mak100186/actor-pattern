using ActorFramework.Abstractions;
using ActorFramework.Models;

using Microsoft.Extensions.Logging;

namespace ActorFramework.Runtime.Infrastructure.Internal;

/// <summary>
/// Base functionality for mailboxes that manage message queues for actors.
/// </summary>
public abstract class Mailbox(ILogger logger) : IMailbox
{
    protected readonly ILogger Logger = logger;

    protected long Pending;

    public int Count => (int)Interlocked.Read(ref Pending);

    public void Start() { /* no extra bootstrapping */ }

    public virtual void Stop() { /* no extra bootstrapping */ }

    public abstract ValueTask EnqueueAsync(IMessage message, CancellationToken cancellationToken);

    public abstract IAsyncEnumerable<MailboxTransaction> DequeueAsync(CancellationToken cancellationToken);

    // Commit the transaction by removing the message from the queue
    protected virtual void OnCommitInternal(IMessage message) => Interlocked.Decrement(ref Pending);

    // Release the signal to unblock any waiting Dequeue calls
    protected virtual void OnRollbackInternal(IMessage message) { }

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

    public abstract MailboxStateExternal GetState();
}