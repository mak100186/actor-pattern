using ActorFramework.Abstractions;
using ActorFramework.Constants;
using ActorFramework.Models;

using Microsoft.Extensions.Logging;

namespace ActorFramework.Runtime.Orchestration;

/// <summary>
/// Contains the dispatch loop logic for actors and retry policies.
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public abstract partial class BaseDirector<TMessage>
    where TMessage : class, IMessage
{
    protected async Task DispatchLoopTransactionalAsync(IActor<TMessage> actor, ActorContext<TMessage> context, IMailbox<TMessage> mailbox, ActorState actorState, CancellationToken ct)
    {
        try
        {
            await foreach (var mailboxTransaction in mailbox.DequeueTransactionally(ct))
            {
                // Block here if actor is paused
                actorState.PauseGate.Wait(ct);

                try
                {
                    // Execute actor logic with retry
                    await actorState.RetryPolicy.ExecuteAsync(
                        async token =>
                        {
                            actorState.RegisterLastMessageReceivedTimestamp();

                            // ConfigureAwait so that you don’t capture a SynchronizationContext or “sticky” context from the caller
                            await actor.OnReceive(mailboxTransaction.Message, context, token).ConfigureAwait(false);
                        },
                        ct
                    );

                    //commit the transaction after successful processing
                    await mailboxTransaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await actor.OnError(context.ActorId, mailboxTransaction.Message, ex).ConfigureAwait(false);
                    
                    if (Options.ShouldStopOnUnhandledException)
                    {
                        Logger.LogError(ex, ActorFrameworkConstants.ActorFaultedAfterMaxRetriesPausing, context.ActorId);
                        actorState.Pause(ex);

                        // Rollback the transaction if processing failed
                        logger.LogWarning(ex, ActorFrameworkConstants.ActorRollbackFailedMessage, context.ActorId);
                        await mailboxTransaction.RollbackAsync();

                        //STOP:  no processing further messages
                        break;
                    }
                    else
                    {
                        logger.LogWarning(ex, ActorFrameworkConstants.ActorSkippingFailedMessage, context.ActorId);

                        //commit the transaction even after unsuccessful processing
                        await mailboxTransaction.CommitAsync();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown path
        }
    }
}