using ActorFramework.Abstractions;
using ActorFramework.Runtime.Orchestration;

namespace ActorFramework.Models;

/// <summary>
/// Provides contextual information and capabilities to an actor instance.
/// </summary>
public sealed class ActorContext<TMessage>
    where TMessage : class, IMessage
{
    /// <summary>
    /// The unique identifier of the actor.
    /// </summary>
    public string ActorId { get; }

    /// <summary>
    /// The central orchestrator managing this actor.
    /// </summary>
    public Director<TMessage> Director { get; }

    /// <summary>
    /// Determines if the actor is currently paused: faulted, waiting for recovery, or intentionally paused.
    /// </summary>
    public bool IsPaused { get; private set; }

    /// <summary>
    /// Gets the number of messages currently pending in the actor's mailbox.
    /// This is useful for monitoring and flow control.
    /// </summary>
    public int PendingMessagesCount { get; private set; }

    /// <summary>
    /// The timestamp of the last message received by this actor.
    /// </summary>
    public DateTimeOffset LastMessageReceivedTimestamp { get; private set; }

    public bool HasReceivedMessageWithin(TimeSpan timeSpan) => DateTimeOffset.UtcNow - LastMessageReceivedTimestamp < timeSpan;

    internal ActorContext(string actorId, Director<TMessage> director)
    {
        ActorId = actorId;
        Director = director;
        
    }

    internal void UpdateStats(bool isPaused, int pendingMessagesCount, DateTimeOffset lastMessageReceivedTimestamp)
    {
        IsPaused = isPaused;
        PendingMessagesCount = pendingMessagesCount;
        LastMessageReceivedTimestamp = lastMessageReceivedTimestamp;
    }
}