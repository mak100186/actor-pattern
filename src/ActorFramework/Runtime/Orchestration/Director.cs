using ActorFramework.Abstractions;
using ActorFramework.Configs;
using ActorFramework.Exceptions;
using ActorFramework.Extensions;
using ActorFramework.Models;
using ActorFramework.Runtime.Infrastructure;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static ActorFramework.Constants.ActorFrameworkConstants;

namespace ActorFramework.Runtime.Orchestration;

/// <summary>
/// Central orchestrator that registers actors, routes messages, and manages lifecycles.
/// </summary>
public sealed class Director<TMessage>(IOptions<ActorFrameworkOptions> options, ILogger<Director<TMessage>> logger) : Internal.BaseDirector<TMessage>(options, logger)
    where TMessage : class, IMessage
{
    /// <summary>
    /// Returns the number of pending messages in each actor’s mailbox.
    /// </summary>
    public IReadOnlyDictionary<string, RegistryState> GetRegistryState() => Registry
        .ToDictionary(
            kvp => kvp.Key,
            kvp => new RegistryState(kvp.Value.IsPaused, kvp.Value.Mailbox.Count, kvp.Value.LastMessageReceivedTimestamp.ToRelativeTimeWithLocal(), kvp.Value.LastException.GetExceptionText(kvp.Value.PausedAt)));

    /// <summary>
    /// Registers an actor under a unique identifier, spins up its mailbox and dispatch loop.
    /// </summary>
    /// <param name="actorId">Unique key for this actor instance.</param>
    /// <param name="actorFactory">Factory creating the actor implementation.</param>
    public void RegisterActor(string actorId, Func<IActor<TMessage>> actorFactory)
    {
        ThrowIfDisposed();

        ArgumentException.ThrowIfNullOrWhiteSpace(nameof(actorId));

        if (Registry.ContainsKey(actorId))
        {
            throw new ActorIdAlreadyRegisteredException(actorId);
        }

        IMailbox<TMessage> mailbox = Options.MailboxType switch
        {
            MailboxType.Unbounded => new UnboundedMailbox<TMessage>(logger),
            MailboxType.Bounded => new BoundedMailbox<TMessage>(Options, logger),
            MailboxType.ConcurrentQueue => new ConcurrentQueueMailbox<TMessage>(Options, Logger),

            _ => throw new MailboxTypeNotHandledException(Options.MailboxType, nameof(Director<TMessage>))
        };

        mailbox.Start();

        var actor = actorFactory();
        ActorContext<TMessage> context = new(actorId, this);
        CancellationTokenSource cts = new();
        var retryPolicy = GetRetryPolicy(actorId);

        ActorState actorState = new()
        {
            Mailbox = mailbox,
            Actor = actor,
            Context = context,
            CancellationSource = cts,
            RetryPolicy = retryPolicy
        };

        actorState.DispatchTask = Task.Run(() => DispatchLoopTransactionalAsync(actor, context, mailbox, actorState, cts.Token));

        Registry[actorId] = actorState;
    }

    /// <summary>
    /// Resumes an actor that was previously paused. All queued messages will be processed.
    /// </summary>
    /// <param name="actorId"></param>
    /// <returns></returns>
    public string ResumeActor(string actorId)
    {
        ThrowIfDisposed();

        if (!Registry.TryGetValue(actorId, out var actorState))
        {
            return string.Format(ActorNotFoundFormat, actorId);
        }

        if (!actorState.IsPaused)
        {
            return ActorAlreadyProcessing;
        }

        Logger.LogInformation(ResumingActor, actorId);

        actorState.Resume();

        if (actorState.DispatchTask.IsCompleted)
        {
            actorState.DispatchTask = Task.Run(() => DispatchLoopTransactionalAsync(actorState.Actor, actorState.Context, actorState.Mailbox, actorState, actorState.CancellationSource.Token));
        }

        return ActorResumed;
    }

    /// <summary>
    /// Allows release of actors based on a custom condition. This logic is outside the scope of the director so that it can be used in different scenarios, such as testing or cleanup.
    /// </summary>
    /// <param name="shouldReleaseHandler">Allows the client to study the Actor details like metadata and mailbox count and then decide whether it can be released. There could be messages getting processed while this method is called. So its up to the client to decide.</param>
    public int ReleaseActors(Func<ActorContext<TMessage>, bool> shouldReleaseHandler)
    {
        var countReleased = 0;
        foreach (var (actorId, actorState) in Registry)
        {
            //provide the latest stats to the actor context
            actorState.Context.UpdateStats(
                actorState.IsPaused,
                actorState.Mailbox.Count,
                actorState.LastMessageReceivedTimestamp);

            //see if client wants to release this actor
            if (shouldReleaseHandler(actorState.Context) && Registry.TryRemove(actorId, out _))
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

        if (!Registry.TryGetValue(actorId, out var actorState))
        {
            throw new ActorIdNotFoundException(actorId);
        }

        if (actorState.IsPaused)
        {
            throw new ActorPausedException(actorId);
        }

        //It blocks the current thread until the event is signaled via Set().
        //If the event is already set, Wait() returns immediately.
        actorState.PauseGate.Wait(actorState.CancellationSource.Token);

        // backpressure will apply if mailbox is full
        return actorState.Mailbox.EnqueueAsync(message);
    }
}