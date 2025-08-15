using ActorFramework.Abstractions;
using ActorFramework.Constants;
using ActorFramework.Models;

using Microsoft.Extensions.Logging;

namespace ActorFramework.Runtime.Orchestration.Internal;

/// <summary>
/// Contains the dispatch loop logic for actors and retry policies.
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public abstract partial class BaseDirector
{
    protected async Task DispatchLoopTransactionalAsync(IActor actor, ActorContext context, IMailbox mailbox, ActorState actorState, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (Infrastructure.Internal.MailboxTransaction mailboxTransaction in mailbox.DequeueAsync(cancellationToken))
            {
                Logger.LogInformation("Director {Identifier} running on Thread {ThreadId}", Identifier, Environment.CurrentManagedThreadId);

                context.Director.RegisterLastActiveTimestamp();

                // Block here if actor is paused
                actorState.PauseGate.Wait(cancellationToken);

                try
                {
                    // Execute actor logic with retry
                    await actorState.RetryPolicy.ExecuteAsync(
                        async token =>
                        {
                            actorState.RegisterLastMessageReceivedTimestamp();

                            // ConfigureAwait so that you don’t capture a SynchronizationContext or “sticky” context from the caller
                            await actor.OnReceive(mailboxTransaction.Message, context.ToExternal(), token).ConfigureAwait(false);
                        },
                        cancellationToken
                    );

                    //commit the transaction after successful processing
                    if (!await mailboxTransaction.CommitAsync())
                    {
                        Logger.LogWarning(ActorFrameworkConstants.CommitFailedAsMessageWasNotAtHeadOfQueue);
                    }
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
                        if (!await mailboxTransaction.CommitAsync())
                        {
                            Logger.LogWarning(ActorFrameworkConstants.CommitFailedAsMessageWasNotAtHeadOfQueue);
                        }
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