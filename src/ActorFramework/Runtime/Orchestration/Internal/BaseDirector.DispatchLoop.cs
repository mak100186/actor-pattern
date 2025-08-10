using ActorFramework.Abstractions;
using ActorFramework.Constants;
using ActorFramework.Models;

using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

namespace ActorFramework.Runtime.Orchestration;

/// <summary>
/// Contains the dispatch loop logic for actors and retry policies.
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public abstract partial class BaseDirector<TMessage> 
    where TMessage : class, IMessage
{
    protected AsyncRetryPolicy GetRetryPolicy(string actorId) => Policy
        .Handle<Exception>()
        .RetryAsync(
            Options.RetryCountIfExceptionOccurs,
            onRetry: (ex, attempt, ctxPol) =>
            {
                Logger.LogWarning(ex, ActorFrameworkConstants.ActorRetryingOnMessage, actorId, attempt, Options.RetryCountIfExceptionOccurs);
            });
    
    protected async Task DispatchLoopAsync(IActor<TMessage> actor, ActorContext<TMessage> context, IMailbox<TMessage> mailbox, ActorState actorState, CancellationToken token)
    {
        try
        {
            await foreach (var message in mailbox.Dequeue(token))
            {
                // Block here if actor is paused
                actorState.PauseGate.Wait(token);

                try
                {
                    // Execute actor logic with retry
                    await actorState.RetryPolicy.ExecuteAsync(
                        async ct =>
                        {
                            actorState.RegisterMessageReceived();

                            // ConfigureAwait so that you don’t capture a SynchronizationContext or “sticky” context from the caller
                            await actor.OnReceive(message, context, ct).ConfigureAwait(false);
                        },
                        token);
                }
                catch (Exception ex)
                {
                    // max retries exceeded
                    await actorState.Actor.OnError(context.ActorId, message, ex);

                    if (actorState.ShouldStopOnError)
                    {
                        Logger.LogError(ex, ActorFrameworkConstants.ActorFaultedAfterMaxRetriesPausing, context.ActorId);

                        actorState.Pause(ex);

                        break;

                        //STOP:  no processing further messages
                    }

                    Logger.LogWarning(ex, ActorFrameworkConstants.ActorSkippingFailedMessageContinuing, context.ActorId);

                    //CONTINUE: swallow and move to next message
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown path
        }
    }
}