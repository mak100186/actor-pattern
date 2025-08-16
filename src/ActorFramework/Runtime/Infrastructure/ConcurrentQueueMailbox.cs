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

public sealed class ConcurrentQueueMailbox(ActorFrameworkOptions actorFrameworkOptions, ILogger logger) : Mailbox(logger)
{
    private readonly ConcurrentQueue<IMessage> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);

    private const int SpinWaitOnBlockedProducerDelayMs = 10;

    /// Maximum number of messages the mailbox can hold.
    private int Capacity { get; } = actorFrameworkOptions?.MailboxCapacity
                                    ?? ActorFrameworkConstants.DefaultMailboxCapacity;

    /// Defines how the mailbox handles overflow when full.
    private OverflowPolicy OverflowPolicy { get; } = actorFrameworkOptions?.MailboxOverflowPolicy
                                                     ?? ActorFrameworkConstants.DefaultOverflowPolicy;

    /// <inheritdoc />
    public override MailboxStateExternal GetState()
    {
        IMessage[] mailboxItems = [.. _queue];
        return new(mailboxItems.Length, mailboxItems);
    }

    public override async ValueTask EnqueueAsync(IMessage message, CancellationToken cancellationToken)
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

    public override async IAsyncEnumerable<MailboxTransaction> DequeueAsync([EnumeratorCancellation] CancellationToken cancellationToken)
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

    // Release the signal to unblock any waiting Dequeue calls
    protected override void OnRollbackInternal(IMessage message) =>
        _signal.Release();

    // unblock any waiting Dequeue calls
    public override void Stop() => _signal.Release(int.MaxValue);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Stop();
            _signal.Dispose();
        }
        base.Dispose(disposing);
    }
}