using ActorFramework.Abstractions;

namespace ActorFramework.Runtime.Orchestration;

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

    internal ActorContext(string actorId, Director<TMessage> director)
    {
        ActorId = actorId;
        Director = director;
    }

    /// <summary>
    /// Sends a message to another actor by its identifier.
    /// </summary>
    /// <param name="targetActorId">Identifier of the target actor.</param>
    /// <param name="message">The message to send.</param>
    public ValueTask Send(string targetActorId, TMessage message) =>
        Director.Send(targetActorId, message);
}