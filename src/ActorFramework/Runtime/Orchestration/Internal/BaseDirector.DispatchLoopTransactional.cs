using ActorFramework.Constants;
using ActorFramework.Events.Poco;
using ActorFramework.Runtime.Infrastructure.Internal;

using Microsoft.Extensions.Logging;

namespace ActorFramework.Runtime.Orchestration.Internal;

/// <summary>
/// Contains the dispatch loop logic for actors and retry policies.
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public abstract partial class BaseDirector
{
    protected async Task DispatchLoopTransactionalAsync(ActorState actorState, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (MailboxTransaction mailboxTransaction in actorState.Mailbox.DequeueAsync(cancellationToken))
            {
                // Block here if actor is paused
                actorState.PauseGate.Wait(cancellationToken);

                eventBus.Publish(new ThreadInformationEvent(Identifier, actorState.Identifier, Environment.CurrentManagedThreadId.ToString()));

                try
                {
                    // Execute actor logic with retry
                    await actorState.RetryPolicy.ExecuteAsync(
                        async token =>
                        {
                            actorState.OnMessageReceived();

                            // ConfigureAwait so that you don’t capture a SynchronizationContext or “sticky” context from the caller
                            await actorState.Actor.OnReceive(mailboxTransaction.Message, actorState.Context.ToExternal(), token).ConfigureAwait(false);
                        },
                        cancellationToken
                    );

                    //commit the transaction after successful processing
                    if (!await mailboxTransaction.CommitAsync())
                    {
                        Logger.LogWarning(ActorFrameworkConstants.CommitFailedAsMessageWasNotAtHeadOfQueue);
                    }

                    actorState.OnMessageCommitted();
                }
                catch (Exception ex)
                {
                    await actorState.Actor.OnError(actorState.Context.ActorId, mailboxTransaction.Message, ex).ConfigureAwait(false);

                    if (Options.ShouldStopOnUnhandledException)
                    {
                        Logger.LogError(ex, ActorFrameworkConstants.ActorFaultedAfterMaxRetriesPausing, actorState.Context.ActorId);

                        await mailboxTransaction.RollbackAsync();

                        actorState.OnMessageFailed(ex);

                        //STOP:  no processing further messages
                        break;
                    }
                    else
                    {
                        logger.LogWarning(ex, ActorFrameworkConstants.ActorSkippingFailedMessage, actorState.Context.ActorId);

                        //commit the transaction even after UN-successful processing
                        if (!await mailboxTransaction.CommitAsync())
                        {
                            Logger.LogWarning(ActorFrameworkConstants.CommitFailedAsMessageWasNotAtHeadOfQueue);
                        }

                        actorState.OnMessageCommitted();
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