using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using ActorFramework.Abstractions;
using ActorFramework.Configs;
using ActorFramework.Constants;
using ActorFramework.Exceptions;
using ActorFramework.Runtime.Infrastructure.Internal;

using Microsoft.Extensions.Logging;

namespace ActorFramework.Runtime.Infrastructure;

public sealed class ConcurrentQueueMailbox<TMessage>(ActorFrameworkOptions actorFrameworkOptions, ILogger logger) : Mailbox<TMessage>(logger)
    where TMessage : class, IMessage
{
    private readonly ConcurrentQueue<TMessage> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);

    private const int PollDelayMs = 10;  // adjust if you need a different spin-wait

    /// Maximum number of messages the mailbox can hold.
    private int Capacity { get; } = actorFrameworkOptions?.MailboxCapacity
                                    ?? ActorFrameworkConstants.DefaultMailboxCapacity;

    /// Defines how the mailbox handles overflow when full.
    private OverflowPolicy OverflowPolicy { get; } = actorFrameworkOptions?.MailboxOverflowPolicy
                                                     ?? ActorFrameworkConstants.DefaultOverflowPolicy;

    public override async ValueTask EnqueueAsync(TMessage message, CancellationToken cancellationToken)
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
                        await Task.Delay(PollDelayMs, cancellationToken).ConfigureAwait(false);
                    }
                    break;

                case OverflowPolicy.DropNewest:
                    Logger.LogInformation(ActorFrameworkConstants.EnqueueOpDropNewest);
                    return;

                case OverflowPolicy.DropOldest:
                    if (_queue.TryDequeue(out var _))
                    {
                        Logger.LogInformation(ActorFrameworkConstants.EnqueueOpDropOldest);
                        Interlocked.Decrement(ref Pending);
                    }
                    break;

                case OverflowPolicy.FailFast:
                    throw new MailboxFullException();

                default:
                    throw new OverflowPolicyNotHandledException(OverflowPolicy, nameof(ConcurrentQueueMailbox<TMessage>));
            }
        }

        // Enqueue and signal the dispatcher
        _queue.Enqueue(message);
        Interlocked.Increment(ref Pending);
        _signal.Release();
    }
    
    public override async IAsyncEnumerable<MailboxTransaction<TMessage>> DequeueTransactionally([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);

            if (_queue.TryPeek(out var msg))
            {
                yield return new MailboxTransaction<TMessage>(
                    _queue,
                    msg,
                    Logger,
                    onCommit: () => Interlocked.Decrement(ref Pending),
                    onRollback: () => _signal.Release() // re-signal for retry
                );
            }
        }
    }

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