using System.Collections.Concurrent;

using ActorFramework.Abstractions;
using ActorFramework.Configs;
using ActorFramework.Exceptions;
using ActorFramework.Runtime.Infrastructure;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;

namespace ActorFramework.Runtime.Orchestration;

/// <summary>
/// Central orchestrator that registers actors, routes messages, and manages lifecycles.
/// </summary>
public sealed class Director<TMessage>(IOptions<ActorFrameworkOptions> options, ILogger<Director<TMessage>> logger) : IDisposable
    where TMessage : class, IMessage
{
    // In-memory registry of all actors
    private readonly ConcurrentDictionary<string, ActorRegistration> _registry = new();
    
    private AsyncRetryPolicy GetRetryPolicy(string actorId) => Policy
        .Handle<Exception>()
        .RetryAsync(
            options.Value.RetryCountIfExceptionOccurs,
            onRetry: (ex, attempt, ctxPol) =>
            {
                logger.LogWarning(ex, "Actor '{ActorId}' retry {Attempt}/{RetryCount} on message", actorId, attempt, options.Value.RetryCountIfExceptionOccurs);
            });

    /// <summary>
    /// Returns the number of pending messages in each actor’s mailbox.
    /// </summary>
    public IReadOnlyDictionary<string, int> GetMailboxStatuses() => _registry
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
    public void RegisterActor(string actorId, Func<IActor<TMessage>> actorFactory, Action<IMessage, Exception>? onMessageError = null)
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

        var registration = new ActorRegistration
        {
            Mailbox = mailbox,
            Actor = actor,
            Context = context,
            CancellationSource = cts,
            RetryPolicy = retryPolicy,
            ShouldStopOnError = options.Value.ShouldStopOnUnhandledException,
            OnMessageError = onMessageError ?? ((message, ex) =>
            {
                logger.LogError(ex, "Unhandled exception in actor {ActorId} processing message {MessageType}",
                    actorId, message.GetType().Name);
            })
        };

        registration.DispatchTask = Task.Run(() => DispatchLoopAsync(actor, context, mailbox, registration, cts.Token));

        _registry[actorId] = registration;
    }
    
    /// <summary>
    /// Delivers a message to the specified actor’s mailbox.
    /// </summary>
    /// <param name="actorId">Target actor’s identifier.</param>
    /// <param name="message">The message to deliver.</param>
    public ValueTask Send(string actorId, TMessage message)
    {
        ThrowIfDisposed();

        if (!_registry.TryGetValue(actorId, out var registration))
        {
            throw new ActorIdNotFoundException(actorId);
        }

        //dont enqueue if actor is paused
        registration.PauseGate.Wait(registration.CancellationSource.Token);

        // backpressure will apply if mailbox is full
        return registration.Mailbox.EnqueueAsync(message);
    }
    
    public string ResumeActor(string actorId)
    {
        ThrowIfDisposed();

        if (!_registry.TryGetValue(actorId, out var reg))
        {
            return $"Actor '{actorId}' not found.";
        }

        if (!reg.IsPaused)
        {
            return "Actor already processing.";
        }

        logger.LogInformation("Resuming actor '{ActorId}'", actorId);
        reg.IsPaused = false;
        reg.PauseGate.Set();

        if (reg.DispatchTask.IsCompleted)
        {
            reg.DispatchTask = Task.Run(() =>
                DispatchLoopAsync(reg.Actor, reg.Context, reg.Mailbox, reg, reg.CancellationSource.Token));
        }

        return "Actor resumed.";
    }

    private async Task DispatchLoopAsync(
        IActor<TMessage> actor,
        ActorContext<TMessage> context,
        IMailbox<TMessage> mailbox, 
        ActorRegistration reg,
        CancellationToken token)
    {
        try
        {
            await foreach (var message in mailbox.Dequeue(token))
            {
                // Block here if actor is paused
                reg.PauseGate.Wait(token);

                try
                {
                    // Execute actor logic with retry
                    await reg.RetryPolicy.ExecuteAsync(
                        async ct => {

                            // ConfigureAwait so that you don’t capture a SynchronizationContext or “sticky” context from the caller
                            await actor.OnReceive(message, context)
                                .ConfigureAwait(false);
                        },
                        token);
                }
                catch (Exception ex)
                {
                    // max retries exceeded
                    reg.OnMessageError?.Invoke(message, ex);

                    if (reg.ShouldStopOnError)
                    {
                        logger.LogError(
                            ex,
                            "Actor '{ActorId}' faulted on message after max retries; pausing",
                            context.ActorId);

                        reg.IsPaused = true;
                        reg.PauseGate.Reset();
                        break; // stop processing further messages
                    }
                    else
                    {
                        logger.LogWarning(
                            ex,
                            "Actor '{ActorId}' skipping failed message and continuing",
                            context.ActorId);
                        continue;  // swallow and move to next message
                    }
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
    private class ActorRegistration
    {
        public IMailbox<TMessage> Mailbox { get; init; }
        public IActor<TMessage> Actor { get; init; }
        public ActorContext<TMessage> Context { get; init; }
        public CancellationTokenSource CancellationSource { get; init; }
        public Task DispatchTask { get; set; }

        //used for pausing/resuming message processing
        public ManualResetEventSlim PauseGate { get; } = new(true);
        public bool IsPaused { get; set; }

        //used for retry policies and error handling
        public AsyncRetryPolicy RetryPolicy { get; init; }
        public bool ShouldStopOnError { get; init; }
        public Action<IMessage, Exception> OnMessageError { get; init; }
    }

    #region Disposal

    private bool _disposed;

    /// <summary>
    /// Gracefully stops all actors and tears down internal resources.
    /// </summary>
    private void Shutdown()
    {
        if (_disposed)
        {
            return;
        }

        logger.LogInformation("Shutting down Director, cancelling actors...");
        foreach (var registration in _registry.Values)
        {
            registration.CancellationSource.Cancel();
        }

        Task.WhenAll(_registry.Values
                .Select(r => r.DispatchTask))
            .GetAwaiter()
            .GetResult();

        logger.LogInformation("Dispatch loops completed, disposing mailboxes...");

        foreach (var registration in _registry.Values)
        {
            registration.Mailbox.Dispose();
            registration.CancellationSource.Dispose();
            registration.PauseGate.Dispose();
        }

        _registry.Clear();
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
            Shutdown();
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