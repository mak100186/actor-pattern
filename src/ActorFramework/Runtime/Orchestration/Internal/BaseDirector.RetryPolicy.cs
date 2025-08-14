using ActorFramework.Abstractions;
using ActorFramework.Constants;

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
}

