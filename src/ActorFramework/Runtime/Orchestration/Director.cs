using System.Collections.Concurrent;

using ActorFramework.Abstractions;
using ActorFramework.Configs;
using ActorFramework.Exceptions;
using ActorFramework.Runtime.Infrastructure;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;

using static ActorFramework.Constants.ActorFrameworkConstants;

namespace ActorFramework.Runtime.Orchestration;

/// <summary>
/// Central orchestrator that actorStateisters actors, routes messages, and manages lifecycles.
/// </summary>
public sealed class Director<TMessage>(IOptions<ActorFrameworkOptions> options, ILogger<Director<TMessage>> logger) : IDisposable
    where TMessage : class, IMessage
{
    // In-memory actorState of all actors
    private readonly ConcurrentDictionary<string, ActorState> _registry = new();
    
    private AsyncRetryPolicy GetRetryPolicy(string actorId) => Policy
        .Handle<Exception>()
        .RetryAsync(
            options.Value.RetryCountIfExceptionOccurs,
            onRetry: (ex, attempt, ctxPol) =>
            {
                logger.LogWarning(ex, ActorRetryingOnMessage, actorId, attempt, options.Value.RetryCountIfExceptionOccurs);
            });

    /// <summary>
    /// Returns the number of pending messages in each actor’s mailbox.
    /// </summary>
    public IReadOnlyDictionary<string, int> GetRegistryState() => _registry
        .ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Mailbox.Count);

    /// <summary>
    /// Registers an actor under a unique identifier, spins up its mailbox and dispatch loop.
    /// </summary>
    /// <param name="actorId">Unique key for this actor instance.</param>
    /// <param name="actorFactory">Factory creating the actor implementation.</param>
    /// <param name="onMessageError">Client can provide a callback and may choose to react to it. If not provided then a default one is used to log the details.
    /// Note: the pause is a <see cref="ManualResetEventSlim"/> that has to be resumed to allow the process to continue.
    /// </param>
    public void RegisterActor(string actorId, Func<IActor<TMessage>> actorFactory)
    {
        ThrowIfDisposed();

        ArgumentException.ThrowIfNullOrWhiteSpace(nameof(actorId));

        if (_registry.ContainsKey(actorId))
        {
            throw new ActorIdAlreadyRegisteredException(actorId);
        }
        
        IMailbox<TMessage> mailbox = options.Value.MailboxType switch
        {
            MailboxType.Unbounded => new UnboundedMailbox<TMessage>(),
            MailboxType.Bounded => new BoundedMailbox<TMessage>(
                options),
            _ => throw new MailboxTypeNotHandledException(options.Value.MailboxType, nameof(Director<TMessage>))
        };

        mailbox.Start();

        var actor = actorFactory();
        var context = new ActorContext<TMessage>(actorId, this);
        var cts = new CancellationTokenSource();
        var retryPolicy = GetRetryPolicy(actorId);

        var actorState = new ActorState
        {
            Mailbox = mailbox,
            Actor = actor,
            Context = context,
            CancellationSource = cts,
            RetryPolicy = retryPolicy,
            ShouldStopOnError = options.Value.ShouldStopOnUnhandledException
        };

        actorState.DispatchTask = Task.Run(() => DispatchLoopAsync(actor, context, mailbox, actorState, cts.Token));
        
        _registry[actorId] = actorState;
    }
    
    public string ResumeActor(string actorId)
    {
        ThrowIfDisposed();

        if (!_registry.TryGetValue(actorId, out var actorState))
        {
            return string.Format(ActorNotFoundFormat, actorId);
        }

        if (!actorState.IsPaused)
        {
            return ActorAlreadyProcessing;
        }

        logger.LogInformation(ResumingActor, actorId);
        actorState.IsPaused = false;
        actorState.PauseGate.Set();

        if (actorState.DispatchTask.IsCompleted)
        {
            actorState.DispatchTask = Task.Run(() => DispatchLoopAsync(actorState.Actor, actorState.Context, actorState.Mailbox, actorState, actorState.CancellationSource.Token));
        }

        return ActorResumed;
    }

    /// <summary>
    /// Allows release of actors based on a custom condition. This logic is outside the scope of the director so that it can be used in different scenarios, such as testing or cleanup.
    /// </summary>
    /// <param name="shouldReleaseHandler">Allows the client to study the Actor details like metadata and mailbox count and then decide whether it can be released. There could be messages getting processed while this method is called. So its upto the client to decide.</param>
    public int ReleaseActors(Func<ActorContext<TMessage>, bool> shouldReleaseHandler)
    {
        var countReleased = 0;
        foreach (var (actorId, actorState) in _registry)
        {
            //provide the latest stats to the actor context
            actorState.Context.UpdateStats(
                actorState.IsPaused,
                actorState.Mailbox.Count,
                actorState.LastMessageReceivedTimestamp);

            //see if client wants to release this actor
            if (shouldReleaseHandler(actorState.Context) && _registry.TryRemove(actorId, out _))
            {
                actorState.Dispose();
                countReleased++;
            }
        }

        return countReleased;
    }

    /// <summary>
    /// Delivers a message to the specified actor’s mailbox.
    /// </summary>
    /// <param name="actorId">Target actor’s identifier.</param>
    /// <param name="message">The message to deliver.</param>
    public ValueTask Send(string actorId, TMessage message)
    {
        ThrowIfDisposed();

        if (!_registry.TryGetValue(actorId, out var actorState))
        {
            throw new ActorIdNotFoundException(actorId);
        }

        //don't enqueue if actor is paused
        //It blocks the current thread until the event is signaled via Set().
        //If the event is already set, Wait() returns immediately.
        actorState.PauseGate.Wait(actorState.CancellationSource.Token);

        // backpressure will apply if mailbox is full
        return actorState.Mailbox.EnqueueAsync(message);
    }
    
    private async Task DispatchLoopAsync(IActor<TMessage> actor, ActorContext<TMessage> context, IMailbox<TMessage> mailbox, ActorState actorState, CancellationToken token)
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
                        async ct => {

                            actorState.LastMessageReceivedTimestamp = DateTimeOffset.UtcNow;

                            // ConfigureAwait so that you don’t capture a SynchronizationContext or “sticky” context from the caller
                            await actor.OnReceive(message, context).ConfigureAwait(false);
                        },
                        token);
                }
                catch (Exception ex)
                {
                    // max retries exceeded
                    await actorState.Actor.OnError(context.ActorId, message, ex);

                    if (actorState.ShouldStopOnError)
                    {
                        logger.LogError(ex, ActorFaultedAfterMaxRetriesPausing, context.ActorId);

                        actorState.IsPaused = true;
                        actorState.PauseGate.Reset(); //closes the gate
                        break; 
                        //STOP:  no processing further messages
                    }

                    logger.LogWarning(ex, ActorSkippingFailedMessageContinuing, context.ActorId);

                    //CONTINUE: swallow and move to next message
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown path
        }
    }

    /// <summary>
    /// Internal holder of per-actor resources.
    /// </summary>
    private sealed class ActorState : IDisposable
    {
        public IMailbox<TMessage> Mailbox { get; init; }
        public IActor<TMessage> Actor { get; init; }
        public ActorContext<TMessage> Context { get; init; }
        public CancellationTokenSource CancellationSource { get; init; }
        public Task DispatchTask { get; set; }

        //used for pausing/resuming message processing - pause is a word used to indicate that the actor is not processing messages at the moment.
        //this is not a blocking pause, but rather a signal to the dispatch loop to wait until it is resumed.
        public ManualResetEventSlim PauseGate { get; } = new(true);
        public bool IsPaused { get; set; }

        //used for retry policies and error handling
        public AsyncRetryPolicy RetryPolicy { get; init; }
        public bool ShouldStopOnError { get; init; }

        public DateTimeOffset LastMessageReceivedTimestamp { get; set; } = DateTimeOffset.MinValue;

        private async Task DisposeInternal()
        {
            try
            {
                try
                {
                    await CancellationSource.CancelAsync();
                }
                catch
                {
                    //ignore cancellation exceptions
                }

                Task.WhenAll(DispatchTask).GetAwaiter().GetResult();

                Mailbox.Stop();
                CancellationSource.Dispose();
                PauseGate.Dispose();
            }
            catch
            {
                //ignore exceptions during shutdown
            }
        }

        private bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (disposing)
            {
                DisposeInternal().GetAwaiter().GetResult();
            }
        }

        ~ActorState() => Dispose(false); // Finalizer calls Dispose(false)
    }

    #region Disposal

    private bool _disposed;

    /// <summary>
    /// Gracefully stops all actors and tears down internal resources.
    /// </summary>
    private async Task ShutdownAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            logger.LogInformation(ShuttingDownDirectorCancellingActors);
            foreach (var actorState in _registry.Values)
            {
                try
                {
                    await actorState.CancellationSource.CancelAsync();
                }
                catch
                {
                    //ignore
                }
            }

            await Task.WhenAll(_registry.Values.Select(r => r.DispatchTask));

            logger.LogInformation(DispatchLoopsCompletedDisposingMailboxes);

            foreach (var actorState in _registry.Values)
            {
                actorState.Mailbox.Dispose();
                actorState.CancellationSource.Dispose();
                actorState.PauseGate.Dispose();
            }

            _registry.Clear();
        }
        catch
        {
            //ignore exceptions during shutdown
        }
    }

    ~Director() => Dispose(false); // Finalizer calls Dispose(false)

    // Public Dispose calls Dispose(true) + suppress finalizer
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            ShutdownAsync().GetAwaiter().GetResult();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(Director<TMessage>));
        }
    }

    #endregion
}