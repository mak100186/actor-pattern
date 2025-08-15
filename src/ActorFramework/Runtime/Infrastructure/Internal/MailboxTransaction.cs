using System.Collections.Concurrent;

using ActorFramework.Abstractions;

namespace ActorFramework.Runtime.Infrastructure.Internal;

public sealed class MailboxTransaction<TMessage>
    where TMessage : class, IMessage
{
    private readonly ConcurrentQueue<TMessage>? _queue;
    private readonly TMessage _message;
    private readonly Action<TMessage> _onCommit;
    private readonly Action<TMessage> _onRollback;

    internal MailboxTransaction(ConcurrentQueue<TMessage>? queue, TMessage message, Action<TMessage> onCommit, Action<TMessage> onRollback) 
    {
        _queue = queue;
        _message = message;
        _onCommit = onCommit;
        _onRollback = onRollback;
    }

    public TMessage Message => _message;

    public Task<bool> CommitAsync()
    {
        //non-transactional mailboxes (like UnboundedMailbox) will not have a queue, so we can commit immediately
        if (_queue == null || (_queue.TryDequeue(out var dequeued) && ReferenceEquals(dequeued, _message)))
        {
            _onCommit(_message);
            return Task.FromResult(true);
        }
        
        return Task.FromResult(false);
    }

    public Task RollbackAsync()
    {
        _onRollback(_message);
        return Task.CompletedTask;
    }
}