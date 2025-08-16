using ActorFramework.Abstractions;

namespace ActorFramework.Models;

/// <summary>
/// Provides contextual information and capabilities to an actor instance.
/// </summary>
public sealed class ActorContext
{
    /// <summary>
    /// The unique identifier of the actor.
    /// </summary>
    public string ActorId { get; }

    /// <summary>
    /// The central orchestrator managing this actor.
    /// </summary>
    public IDirector Director { get; }

    internal ActorContext(string actorId, IDirector director)
    {
        ActorId = actorId;
        Director = director;
    }

    public ActorContextExternal ToExternal(int pendingMessageCount, DateTimeOffset lastReceivedMessageTimestamp) => new(Director.Identifier, ActorId, pendingMessageCount, lastReceivedMessageTimestamp);
}
