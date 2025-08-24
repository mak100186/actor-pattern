using ActorFramework.Abstractions;
using ActorFramework.Configs;
using ActorFramework.Events;
using ActorFramework.Events.Poco;
using ActorFramework.Exceptions;
using ActorFramework.Extensions;
using ActorFramework.Models;
using ActorFramework.Runtime.Infrastructure;
using ActorFramework.Runtime.Orchestration.Internal;

using Microsoft.Extensions.DependencyInjection;
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

    private readonly IServiceProvider _serviceProvider;
    private readonly ActorRegistrationBuilder _actorRegistrationBuilder;
    private readonly ActorIdProvider _actorIdProvider;

    public Director(
        IServiceProvider serviceProvider,
        ActorIdProvider actorIdProvider,
        ActorRegistrationBuilder actorRegistrationBuilder,
        IOptions<ActorFrameworkOptions> options,
        ILogger<Director> logger,
        IEventBus eventBus)
            : base(options, logger, eventBus)
    {
        _serviceProvider = serviceProvider;
        _actorIdProvider = actorIdProvider;
        _actorRegistrationBuilder = actorRegistrationBuilder;

        eventBus.Register(this);
    }

    public void OnEvent(ActorReceivedMessageEvent evt) => LastActive = DateTimeOffset.UtcNow;

    protected override void Cleanup()
    {
        base.Cleanup();

        EventBus.Unregister(this);
    }

    public bool IsBusy()
    {
        if (Registry.Values.Any(x => x.IsPaused))
        {
            return true;
        }

        if (Registry.Values.Any(x => x.Mailbox.Count > 0))
        {
            return true;
        }

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

        return new(
            Identifier,
            actorStates.Length,
            TotalQueuedMessageCount,
            actorStates,
            IsBusy(),
            LastActive.ToRelativeTimeWithLocal()
        );
    }

    public bool ContainsActor(IMessage message)
    {
        ThrowIfDisposed();

        string actorId = _actorIdProvider.GetActorId(message);

        return Registry.ContainsKey(actorId);
    }

    public int GetQueuedMessageCountForActor(IMessage message)
    {
        ThrowIfDisposed();
        string actorId = _actorIdProvider.GetActorId(message);
        if (Registry.TryGetValue(actorId, out ActorState? actorState))
        {
            return actorState.Mailbox.Count;
        }
        throw new ActorIdNotFoundException(actorId);
    }

    /// <summary>
    /// Delivers a message to the specified actor’s mailbox.
    /// </summary>
    /// <param name="actorId">Target actor’s identifier.</param>
    /// <param name="message">The message to deliver.</param>
    public ValueTask Send(IMessage message)
    {
        ThrowIfDisposed();

        string actorId = _actorIdProvider.GetActorId(message);

        if (!Registry.TryGetValue(actorId, out ActorState? actorState))
        {
            if (_actorRegistrationBuilder.MessageToActorMap.TryGetValue(message.GetType(), out Type? actorType))
            {
                RegisterActor(actorId, () => (IActor)ActivatorUtilities.CreateInstance(_serviceProvider, actorType));
            }

            actorState = Registry.GetValueOrDefault(actorId);

            if (actorState == null)
            {
                throw new ActorIdNotFoundException(actorId);
            }
        }

        EventBus.Publish(new DirectorReceivedMessageEvent(Identifier));

        // backpressure will apply if mailbox is full
        return actorState.Mailbox.EnqueueAsync(message, actorState.CancellationSource.Token);
    }

    /// <summary>
    /// Resumes all actors that were previously paused. All queued messages will be processed.
    /// </summary>
    public void ResumeActors()
    {
        foreach (string actorId in Registry.Keys)
        {
            ResumeActor(actorId);
        }
    }

    private string ResumeActor(string actorId)
    {
        ThrowIfDisposed();

        if (!Registry.TryGetValue(actorId, out ActorState? actorState))
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

    private void RegisterActor(string actorId, Func<IActor> actorFactory)
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

        IActor actor = actorFactory();
        ActorContext context = new(actorId, this);
        CancellationTokenSource cts = new();

        ActorState actorState = new(EventBus, mailbox, actor, context, cts, GetRetryPolicy(actorId), DispatchLoopTransactionalAsync);

        Registry[actorId] = actorState;
    }
}
