using ActorFramework.Models;

namespace ActorFramework.Abstractions;

/// <summary>
/// Defines the contract for an actor: how it receives and processes messages.
/// </summary>
public interface IActor
{
    /// <summary>
    /// Handles an incoming message.
    /// </summary>
    /// <param name="message">The message instance to process.</param>
    /// <param name="context">Provides identity and system access for the current actor.</param>
    /// <param name="cancellationToken">The token for the process</param>
    Task OnReceive(IMessage message, ActorContextExternal context, CancellationToken cancellationToken);

    /// <summary>
    /// Handles errors that occur during message processing.
    /// </summary>
    /// <param name="actorId"></param>
    /// <param name="message"></param>
    /// <param name="exception"></param>
    /// <returns></returns>
    Task OnError(string actorId, IMessage message, Exception exception);
}