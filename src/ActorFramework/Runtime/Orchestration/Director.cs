using ActorFramework.Abstractions;
using ActorFramework.Configs;
using ActorFramework.Events;
using ActorFramework.Events.Poco;
using ActorFramework.Exceptions;
using ActorFramework.Extensions;
using ActorFramework.Models;
using ActorFramework.Runtime.Infrastructure;
using ActorFramework.Runtime.Orchestration.Internal;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static ActorFramework.Constants.ActorFrameworkConstants;

namespace ActorFramework.Runtime.Orchestration;

/// <summary>
/// Central orchestrator that registers actors, routes messages, and manages lifecycles.
/// </summary>
public sealed class Director : BaseDirector,
    IDirector,
    IEventListener<ActorReceivedMessageEvent>
{
    public DateTimeOffset LastActive { get; private set; } = DateTimeOffset.MinValue;

    public int TotalQueuedMessageCount => Registry.Sum(kvp => kvp.Value.Mailbox.Count);

    public Director(IOptions<ActorFrameworkOptions> options, ILogger<Director> logger, IEventBus eventBus) : base(options, logger, eventBus)
    {
        eventBus.Register<ActorReceivedMessageEvent>(this);
    }

    public void OnEvent(ActorReceivedMessageEvent evt)
    {
        LastActive = DateTimeOffset.UtcNow;
    }

    protected override void Cleanup()
    {
        base.Cleanup();

        EventBus.Unregister<ActorReceivedMessageEvent>(this);
    }

    public bool IsBusy()
    {
        if (Registry.Values.Any(x => x.IsPaused))
            return true;

        if (Registry.Values.Any(x => x.Mailbox.Count > 0))
            return true;

        return false;
    }

    /// <summary>
    /// Returns the number of pending messages in each actor’s mailbox.
    /// </summary>
    public DirectorStateExternal GetState()
    {
        ActorStateExternal[] actorStates = [.. Registry.Select(kvp =>
                new ActorStateExternal(
                    kvp.Value.Identifier,
                    kvp.Key,
                    kvp.Value.Mailbox.GetState(),
                    kvp.Value.IsPaused,
                    kvp.Value.LastMessageReceivedTimestamp.ToRelativeTimeWithLocal(),
                    kvp.Value.LastException.GetExceptionText(kvp.Value.PausedAt)))];

        return new DirectorStateExternal
        (
            Identifier,
            actorStates.Length,
            TotalQueuedMessageCount,
            actorStates,
            IsBusy(),
            LastActive.ToRelativeTimeWithLocal()
        );
    }

    /// <summary>
    /// Registers an actor under a unique identifier, spins up its mailbox and dispatch loop.
    /// </summary>
    /// <param name="actorId">Unique key for this actor instance.</param>
    /// <param name="actorFactory">Factory creating the actor implementation.</param>
    public void RegisterActor(string actorId, Func<IActor> actorFactory)
    {
        ThrowIfDisposed();

        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        if (Registry.ContainsKey(actorId))
        {
            throw new ActorIdAlreadyRegisteredException(actorId);
        }

        IMailbox mailbox = Options.MailboxType switch
        {
            MailboxType.ConcurrentQueue => new ConcurrentQueueMailbox(Options, Logger),

            _ => throw new MailboxTypeNotHandledException(Options.MailboxType, nameof(Director))
        };

        var actor = actorFactory();
        ActorContext context = new(actorId, this);
        CancellationTokenSource cts = new();

        ActorState actorState = new(EventBus, mailbox, actor, context, cts, GetRetryPolicy(actorId), DispatchLoopTransactionalAsync);

        Registry[actorId] = actorState;
    }

    /// <summary>
    /// Resumes all actors that were previously paused. All queued messages will be processed.
    /// </summary>
    /// <returns></returns>
    public void ResumeActors()
    {
        foreach (var actorId in Registry.Keys)
        {
            ResumeActor(actorId);
        }
    }

    private string ResumeActor(string actorId)
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

        actorState.Resume(DispatchLoopTransactionalAsync);

        return ActorResumed;
    }

    /// <summary>
    /// Delivers a message to the specified actor’s mailbox.
    /// </summary>
    /// <param name="actorId">Target actor’s identifier.</param>
    /// <param name="message">The message to deliver.</param>
    public ValueTask Send(string actorId, IMessage message)
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
        
        EventBus.Publish(new DirectorReceivedMessageEvent(Identifier));

        // backpressure will apply if mailbox is full
        return actorState.Mailbox.EnqueueAsync(message, actorState.CancellationSource.Token);
    }
}