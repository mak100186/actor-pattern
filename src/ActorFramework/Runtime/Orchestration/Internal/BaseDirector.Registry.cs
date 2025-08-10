using System.Collections.Concurrent;

using ActorFramework.Abstractions;
using ActorFramework.Models;

using Polly.Retry;

namespace ActorFramework.Runtime.Orchestration;

/// <summary>
/// Contains the registry of actors and their states.
/// </summary>
/// <typeparam name="TMessage"></typeparam>
/// <param name="options"></param>
/// <param name="logger"></param>
public abstract partial class BaseDirector<TMessage>
    where TMessage : class, IMessage
{
    // In-memory actorState of all actors
    protected readonly ConcurrentDictionary<string, ActorState> Registry = new();

    /// <summary>
    /// Internal holder of per-actor resources.
    /// </summary>
    protected sealed class ActorState : IDisposable, IAsyncDisposable
    {
        public IMailbox<TMessage> Mailbox { get; init; }
        public IActor<TMessage> Actor { get; init; }
        public ActorContext<TMessage> Context { get; init; }
        public CancellationTokenSource CancellationSource { get; init; }
        public Task DispatchTask { get; set; }

        //used for pausing/resuming message processing - pause is a word used to indicate that the actor is not processing messages at the moment.
        //this is not a blocking pause, but rather a signal to the dispatch loop to wait until it is resumed.
        public ManualResetEventSlim PauseGate { get; } = new(true);
        public bool IsPaused { get; private set; }
        public Exception? LastException { get; private set; }
        public DateTimeOffset? PausedAt { get; private set; }

        //used for retry policies and error handling
        public AsyncRetryPolicy RetryPolicy { get; init; }
        public bool ShouldStopOnError { get; init; }

        public DateTimeOffset LastMessageReceivedTimestamp { get; private set; } = DateTimeOffset.MinValue;

        public void Pause(Exception? ex)
        {
            IsPaused = true;
            PauseGate.Reset(); //closes the gate until manually resumed by Set()

            if (ex != null)
            {
                LastException = ex;
                PausedAt = DateTimeOffset.UtcNow;
            }
        }

        public void Resume()
        {
            IsPaused = false;
            PauseGate.Set();
            LastException = null;
            PausedAt = null;
        }

        public void RegisterMessageReceived()
        {
            LastMessageReceivedTimestamp = DateTimeOffset.UtcNow;
        }

    #region Disposal

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

                await DispatchTask;

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