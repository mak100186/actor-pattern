using System.Collections.Concurrent;

using ActorFramework.Abstractions;
using ActorFramework.Constants;

using Microsoft.Extensions.Logging;

namespace ActorFramework.Runtime.Infrastructure.Internal;

public sealed class MailboxTransaction<TMessage>
    where TMessage : class, IMessage
{
    private readonly ConcurrentQueue<TMessage>? _queue;
    private readonly TMessage _message;
    private readonly ILogger _logger;
    private readonly Action _onCommit;
    private readonly Action _onRollback;

    internal MailboxTransaction(ConcurrentQueue<TMessage>? queue, TMessage message, ILogger logger, Action onCommit, Action onRollback) 
    {
        _queue = queue;
        _message = message;
        _logger = logger;
        _onCommit = onCommit;
        _onRollback = onRollback;
    }

    public TMessage Message => _message;

    public Task CommitAsync()
    {
        //non-transactional mailboxes (like UnboundedMailbox) will not have a queue, so we can commit immediately
        if (_queue == null || (_queue.TryDequeue(out var dequeued) && ReferenceEquals(dequeued, _message)))
        {
            _onCommit();
            return Task.CompletedTask;
        }

        _logger.LogWarning(ActorFrameworkConstants.CommitFailedAsMessageWasNotAtHeadOfQueue);
        return Task.CompletedTask;
    }

    public Task RollbackAsync()
    {
        _onRollback();
        return Task.CompletedTask;
    }
}