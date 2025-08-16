using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using ActorFramework.Abstractions;
using ActorFramework.Configs;
using ActorFramework.Constants;
using ActorFramework.Exceptions;
using ActorFramework.Models;
using ActorFramework.Runtime.Infrastructure.Internal;

using Microsoft.Extensions.Logging;

namespace ActorFramework.Runtime.Infrastructure;

public sealed partial class ConcurrentQueueMailbox(ActorFrameworkOptions actorFrameworkOptions, ILogger logger)
{
    private readonly ConcurrentQueue<IMessage> _queue = new();

    private const int SpinWaitOnBlockedProducerDelayMs = 10;

    /// Maximum number of messages the mailbox can hold.
    private int Capacity { get; } = actorFrameworkOptions?.MailboxCapacity
                                    ?? ActorFrameworkConstants.DefaultMailboxCapacity;

    /// Defines how the mailbox handles overflow when full.
    private OverflowPolicy OverflowPolicy { get; } = actorFrameworkOptions?.MailboxOverflowPolicy
                                                     ?? ActorFrameworkConstants.DefaultOverflowPolicy;

    private readonly ILogger Logger = logger;

    private long Pending;

    private readonly SemaphoreSlim _signal = new(0);

    // Commit the transaction by removing the message from the queue
    private void OnCommitInternal(IMessage message) =>
        Interlocked.Decrement(ref Pending);

    // Release the signal to unblock any waiting Dequeue calls
    private void OnRollbackInternal(IMessage message) =>
        _signal.Release();

    // unblock any waiting Dequeue calls
    private void Stop() => _signal.Release(int.MaxValue);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            Stop();
            _signal.Dispose();
        }
    }

}
public sealed partial class ConcurrentQueueMailbox
    : IMailbox
{
    public int Count => (int)Interlocked.Read(ref Pending);
    /// <inheritdoc />
    public MailboxStateExternal GetState()
    {
        IMessage[] mailboxItems = [.. _queue];
        return new(mailboxItems.Length, mailboxItems);
    }

    public async ValueTask EnqueueAsync(IMessage message, CancellationToken cancellationToken)
    {
        // Backpressure / overflow logic
        if (Capacity > 0 && Interlocked.Read(ref Pending) >= Capacity)
        {
            switch (OverflowPolicy)
            {
                case OverflowPolicy.BlockProducer:
                    Logger.LogInformation(ActorFrameworkConstants.EnqueueOpBlockedAsMailboxAtCapacity);

                    // spin-wait until there's room
                    while (Interlocked.Read(ref Pending) >= Capacity)
                    {
                        await Task.Delay(SpinWaitOnBlockedProducerDelayMs, cancellationToken).ConfigureAwait(false);
                    }
                    break;

                case OverflowPolicy.DropNewest:
                    Logger.LogInformation(ActorFrameworkConstants.EnqueueOpDropNewest, message);
                    return;

                case OverflowPolicy.DropOldest:
                    if (_queue.TryDequeue(out IMessage? dequeuedMessage))
                    {
                        Logger.LogInformation(ActorFrameworkConstants.EnqueueOpDropOldest, dequeuedMessage);
                        Interlocked.Decrement(ref Pending);
                    }
                    break;

                case OverflowPolicy.FailFast:
                    Logger.LogInformation(ActorFrameworkConstants.EnqueueOpMailboxFull, message);
                    throw new MailboxFullException();

                default:
                    throw new OverflowPolicyNotHandledException(OverflowPolicy, nameof(ConcurrentQueueMailbox));
            }
        }

        // Enqueue and signal the dispatcher
        _queue.Enqueue(message);
        Interlocked.Increment(ref Pending);
        _signal.Release();
    }

    public async IAsyncEnumerable<MailboxTransaction> DequeueAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);

            if (_queue.TryPeek(out IMessage? message))
            {
                yield return new(_queue, message, OnCommitInternal, OnRollbackInternal);
            }
        }
    }
}