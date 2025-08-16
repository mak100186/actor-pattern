using System.Collections.Concurrent;

using ActorFramework.Abstractions;
using ActorFramework.Base;
using ActorFramework.Events;
using ActorFramework.Events.Poco;
using ActorFramework.Models;

using Polly.Retry;

namespace ActorFramework.Runtime.Orchestration.Internal;

/// <summary>
/// Contains the registry of actors and their states.
/// </summary>
/// <typeparam name="TMessage"></typeparam>
/// <param name="options"></param>
/// <param name="logger"></param>
public abstract partial class BaseDirector
{
    // In-memory actorState of all actors
    protected readonly ConcurrentDictionary<string, ActorState> Registry = new();

    /// <summary>
    /// Internal holder of per-actor resources.
    /// </summary>
    protected sealed class ActorState : IdentifiableBase, IDisposable, IAsyncDisposable
    {
        public IEventBus EventBus { get; init; }
        public IMailbox Mailbox { get; init; }
        public IActor Actor { get; init; }
        public ActorContext Context { get; init; }
        public CancellationTokenSource CancellationSource { get; init; }
        public AsyncRetryPolicy RetryPolicy { get; init; }
        public Task DispatchTask { get; set; }

        //used for pausing/resuming message processing - pause is a word used to indicate that the actor is not processing messages at the moment.
        //this is not a blocking pause, but rather a signal to the dispatch loop to wait until it is resumed.
        public ManualResetEventSlim PauseGate { get; } = new(true);
        public bool IsPaused { get; private set; }
        public Exception? LastException { get; private set; }
        public DateTimeOffset? PausedAt { get; private set; }
        public DateTimeOffset LastMessageReceivedTimestamp { get; private set; } = DateTimeOffset.MinValue;
        public int PendingMessageCount => Mailbox.Count;

        public ActorState(IEventBus eventBus, IMailbox mailbox, IActor actor, ActorContext context, CancellationTokenSource cancellationSource, AsyncRetryPolicy retryPolicy, Func<ActorState, CancellationToken, Task> dispatchLoop)
        {
            this.EventBus = eventBus;
            this.Mailbox = mailbox;
            this.Actor = actor;
            this.Context = context;
            this.CancellationSource = cancellationSource;
            this.RetryPolicy = retryPolicy;
            this.DispatchTask = Task.Run(() => dispatchLoop(this, CancellationSource.Token));

            eventBus.Publish(new ActorRegisteredEvent(context.ActorId, context.Director.Identifier));
        }

        public void Resume(Func<ActorState, CancellationToken, Task> dispatchLoop)
        {
            EventBus.Publish(new ActorResumedEvent(Context.ActorId));

            IsPaused = false;
            PauseGate.Set();
            LastException = null;
            PausedAt = null;

            if (DispatchTask.IsCompleted)
            {
                DispatchTask = Task.Run(() => dispatchLoop(this, CancellationSource.Token));
            }
        }

        public void OnMessageReceived()
        {
            EventBus.Publish(new ActorReceivedMessageEvent(Context.ActorId));

            LastMessageReceivedTimestamp = DateTimeOffset.UtcNow;
        }

        public void OnMessageCommitted()
        {
            // Raise an idle event if the mailbox is empty after processing
            if (Mailbox.Count == 0)
            {
                EventBus.Publish(new ActorIdleEvent(Context.ActorId));
            }
        }

        public void OnMessageFailed(Exception ex)
        {
            EventBus.Publish(new ActorPausedEvent(Context.ActorId, ex));

            IsPaused = true;
            PauseGate.Reset(); //closes the gate until manually resumed by Set()

            if (ex != null)
            {
                LastException = ex;
                PausedAt = DateTimeOffset.UtcNow;
            }
        }

        #region Disposal

        private async Task DisposeInternal()
        {
            try
            {
                EventBus.Publish(new ActorDisposedEvent(Context.ActorId));

                try
                {
                    await CancellationSource.CancelAsync();
                }
                catch
                {
                    //ignore cancellation exceptions
                }

                await DispatchTask;

                Mailbox.Dispose();
                CancellationSource.Dispose();
                PauseGate.Dispose();
            }
            catch
            {
                //ignore exceptions during shutdown
            }
        }

        private bool _disposed;

        ~ActorState() => Dispose(false);

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

        // Async Dispose implementation
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            await DisposeInternal().ConfigureAwait(false);
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}