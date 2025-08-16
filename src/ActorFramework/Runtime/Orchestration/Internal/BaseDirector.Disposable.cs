using ActorFramework.Constants;

using Microsoft.Extensions.Logging;

namespace ActorFramework.Runtime.Orchestration.Internal;

public abstract partial class BaseDirector : IDisposable, IAsyncDisposable
{
    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, typeof(Director));

    protected virtual void Cleanup()
    {
        // This method can be overridden by derived classes to perform additional cleanup.
        // The base implementation does nothing.
    }

    private async Task DisposeInternal()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            Logger.LogInformation(ActorFrameworkConstants.ShuttingDownDirectorCancellingActors);

            Cleanup();

            foreach (ActorState actorState in Registry.Values)
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

            await Task.WhenAll(Registry.Values.Select(r => r.DispatchTask));

            Logger.LogInformation(ActorFrameworkConstants.DispatchLoopsCompletedDisposingMailboxes);

            foreach (ActorState actorState in Registry.Values)
            {
                actorState.Mailbox.Dispose();
                actorState.CancellationSource.Dispose();
                actorState.PauseGate.Dispose();
            }

            Registry.Clear();
        }
        catch
        {
            //ignore exceptions during shutdown
        }
    }

    private bool _disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Synchronously wait for your async cleanup 
            DisposeInternal().GetAwaiter().GetResult();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
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
}