using ActorFramework.Runtime.Orchestration;

namespace ActorFramework.Abstractions;

/// <summary>
/// Defines the contract for an actor: how it receives and processes messages.
/// </summary>
public interface IActor<TMessage>
    where TMessage : class, IMessage
{
    /// <summary>
    /// Handles an incoming message.
    /// </summary>
    /// <param name="message">The message instance to process.</param>
    /// <param name="context">Provides identity and system access for the current actor.</param>
    Task OnReceive(TMessage message, ActorContext<TMessage> context);
}