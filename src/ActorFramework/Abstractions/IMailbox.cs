using ActorFramework.Runtime.Infrastructure.Internal;

namespace ActorFramework.Abstractions;

/// <summary>
/// A thread-safe, pluggable queue for delivering messages to an actor.
/// </summary>
public interface IMailbox<TMessage> : IDisposable where TMessage : class, IMessage
{
    /// <summary>
    /// Begin the mailbox’s internal loops (e.g. dispatch, metrics).
    /// Must be called before EnqueueAsync/DequeueAsync.
    /// </summary>
    void Start();

    /// <summary>
    /// Signal the mailbox to stop accepting new messages and drain/complete loops.
    /// </summary>
    void Stop();

    /// <summary>
    /// Enqueue a message, observing backpressure or overflow policies.
    /// </summary>
    /// <param name="message">The message instance.</param>
    /// <param name="cancellationToken">Cancel waiting if queue is full.</param>
    ValueTask EnqueueAsync(TMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an async stream of messages for the actor to process. Dequeue the next message, waiting if none are available.
    /// </summary>
    /// <param name="cancellationToken">Cancel waiting on shutdown or timeouts.</param>
    IAsyncEnumerable<MailboxTransaction<TMessage>> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Number of messages currently buffered and waiting to be processed.
    /// </summary>
    int Count { get; }
}