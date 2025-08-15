using ActorFramework.Constants;

using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

namespace ActorFramework.Runtime.Orchestration.Internal;

/// <summary>
/// Contains the dispatch loop logic for actors and retry policies.
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public abstract partial class BaseDirector
{
    protected AsyncRetryPolicy GetRetryPolicy(string actorId) => Policy
        .Handle<Exception>(ex => ex is not OperationCanceledException && ex is not TaskCanceledException)
        .RetryAsync(
            Options.RetryCountIfExceptionOccurs,
            onRetry: (ex, attemptNumber, _) =>
            {
                Logger.LogWarning(ex, ActorFrameworkConstants.ActorRetryingOnMessage, actorId, attemptNumber, Options.RetryCountIfExceptionOccurs);
            });
}

