using ActorFramework.Models;
using ActorFramework.Runtime.Infrastructure.Internal;

namespace ActorFramework.Abstractions;

/// <summary>
/// A thread-safe, pluggable queue for delivering messages to an actor.
/// </summary>
public interface IMailbox : IDisposable
{
    /// <summary>
    /// Enqueue a message, observing backpressure or overflow policies.
    /// </summary>
    /// <param name="message">The message instance.</param>
    /// <param name="cancellationToken">Cancel waiting if queue is full.</param>
    ValueTask EnqueueAsync(IMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Returns an async stream of messages for the actor to process. Dequeue the next message, waiting if none are available.
    /// </summary>
    /// <param name="cancellationToken">Cancel waiting on shutdown or timeouts.</param>
    IAsyncEnumerable<MailboxTransaction> DequeueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Number of messages currently buffered and waiting to be processed.
    /// </summary>
    int Count { get; }

    MailboxStateExternal GetState();
}