using System.Collections.Concurrent;

using ActorFramework.Abstractions;

namespace ActorFramework.Runtime.Infrastructure.Internal;

public sealed class MailboxTransaction
{
    private readonly ConcurrentQueue<IMessage>? _queue;
    private readonly Action<IMessage> _onCommit;
    private readonly Action<IMessage> _onRollback;

    internal MailboxTransaction(ConcurrentQueue<IMessage>? queue, IMessage message, Action<IMessage> onCommit, Action<IMessage> onRollback)
    {
        Message = message;

        _queue = queue;
        _onCommit = onCommit;
        _onRollback = onRollback;
    }

    public IMessage Message { get; }

    public Task<bool> CommitAsync()
    {
        //non-transactional mailboxes (like UnboundedMailbox) will not have a queue, so we can commit immediately
        if (_queue == null || (_queue.TryDequeue(out var dequeued) && ReferenceEquals(dequeued, Message)))
        {
            _onCommit(Message);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task RollbackAsync()
    {
        _onRollback(Message);
        return Task.CompletedTask;
    }
}