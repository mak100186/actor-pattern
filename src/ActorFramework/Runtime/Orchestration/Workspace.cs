using ActorFramework.Abstractions;
using ActorFramework.Base;
using ActorFramework.Configs;
using ActorFramework.Constants;
using ActorFramework.Events;
using ActorFramework.Events.Poco;
using ActorFramework.Extensions;
using ActorFramework.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActorFramework.Runtime.Orchestration;

public partial class Workspace : IdentifiableBase, IWorkspace
{
    private readonly ActorRegistrationBuilder _actorRegistrationBuilder;
    private readonly IOptions<ActorFrameworkOptions> _options;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly List<IDirector> _directors = [];
    private readonly Lock _lock = new();

    public Workspace(ActorRegistrationBuilder actorRegistrationBuilder, IOptions<ActorFrameworkOptions> options, ILogger<Workspace> logger, ILoggerFactory loggerFactory, IServiceProvider serviceProvider, IEventBus eventBus)
    {
        _options = options;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _actorRegistrationBuilder = actorRegistrationBuilder;

        _eventBus.Register<ThreadInformationEvent>(this);

        CreateDirector(); // bootstrap 1 director
    }

    public IReadOnlyList<IDirector> Directors
    {
        get { lock (_lock) return [.. _directors]; }
    }

    public IDirector? CreateDirector()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (_directors.Count >= _options.Value.MaxDegreeOfParallelism)
            {
                _eventBus.Publish(new WorkspaceCapacityReachedEvent(Identifier));
                return null;
            }

            Director director = new(_options, _loggerFactory.CreateLogger<Director>(), _eventBus);

            _eventBus.Publish(new DirectorRegisteredEvent(director.Identifier, Identifier));

            foreach (Type actorType in _actorRegistrationBuilder.ActorTypes)
            {
                director.RegisterActor(actorType.Name, () => (IActor)ActivatorUtilities.CreateInstance(_serviceProvider, actorType));
            }

            _directors.Add(director);
            return director;
        }
    }

    public void RemoveDirector(IDirector director)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            _logger.LogInformation(ActorFrameworkConstants.RemovingDirector, director.Identifier);

            _directors.Remove(director);
            director.Dispose();

            _eventBus.Publish(new DirectorDisposedEvent(director.Identifier, Identifier));
        }
    }

    public void Resume()
    {
        ThrowIfDisposed();

        foreach (IDirector director in _directors)
        {
            director.ResumeActors();
        }
    }

    public WorkspaceStateExternal GetState()
    {
        DirectorStateExternal[] directorStates = [.. _directors.Select(x => x.GetState())];
        return new WorkspaceStateExternal(Identifier, directorStates.Length, directorStates);
    }
}

public partial class Workspace : IEventListener<ThreadInformationEvent>
{
    public void OnEvent(ThreadInformationEvent evt)
    {
        _logger.LogInformation(ActorFrameworkConstants.ThreadRunningDirector, evt.ThreadId, evt.DirectorId, evt.ActorId);
    }
}

public partial class Workspace : IDisposable, IAsyncDisposable
{
    private bool _disposed;

    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, GetType());
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

    private Task DisposeInternal()
    {
        try
        {
            _logger.LogInformation(ActorFrameworkConstants.ShuttingDownWorkspaceDisposingDirectors);

            _eventBus.Unregister<ThreadInformationEvent>(this);
            
            foreach (IDirector director in _directors.ToArray())
            {
                RemoveDirector(director);
            }
        }
        catch
        {
            //ignore exceptions during shutdown
        }

        return Task.CompletedTask;
    }
}