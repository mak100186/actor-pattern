using ActorFramework.Abstractions;
using ActorFramework.Base;
using ActorFramework.Configs;
using ActorFramework.Extensions;
using ActorFramework.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActorFramework.Runtime.Orchestration;

public class Workspace : IdentifiableBase, IWorkspace
{
    private readonly ActorRegistrationBuilder _actorRegistrationBuilder;
    private readonly IOptions<ActorFrameworkOptions> _options;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<IDirector> _directors = [];
    private readonly Lock _lock = new();

    public Workspace(ActorRegistrationBuilder actorRegistrationBuilder, IOptions<ActorFrameworkOptions> options, ILogger<Workspace> logger, ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
    {
        _options = options;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _serviceProvider = serviceProvider;
        _actorRegistrationBuilder = actorRegistrationBuilder;

        CreateDirector(); // bootstrap 1 director
    }

    public IReadOnlyList<IDirector> Directors
    {
        get { lock (_lock) return [.. _directors]; }
    }

    public IDirector CreateDirector()
    {
        lock (_lock)
        {
            if (_directors.Count >= _options.Value.MaxDegreeOfParallelism)
                throw new InvalidOperationException("Max directors reached");

            _logger.LogInformation("Creating director");
            Director director = new(_options, _loggerFactory.CreateLogger<Director>());

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
        lock (_lock)
        {
            _logger.LogInformation("Removing director");
            _directors.Remove(director);
            director.Dispose();
        }
    }

    public void Resume()
    {
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