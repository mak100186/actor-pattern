using ActorFramework.Abstractions;
using ActorFramework.Configs;
using ActorFramework.Models;
using ActorFramework.Runtime.Orchestration;

using ActorWorkspace.Abstractions;
using ActorWorkspace.Extensions;
using ActorWorkspace.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActorWorkspace.Runtime.Infrastructure;

public class Workspace : IWorkspace
{
    private readonly ActorRegistrationBuilder _actorRegistrationBuilder;
    private readonly IOptions<ActorFrameworkOptions> _options;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<IDirector<IMessage>> _directors = [];
    private readonly Lock _lock = new();

    public Workspace(ActorRegistrationBuilder actorRegistrationBuilder, IOptions<ActorFrameworkOptions> options, ILogger<Workspace> logger, ILoggerFactory loggerFactory)
    {
        _options = options;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _actorRegistrationBuilder = actorRegistrationBuilder;

        _directors.Add(CreateDirector()); // bootstrap 1 director
    }

    public IReadOnlyList<IDirector<IMessage>> Directors
    {
        get { lock (_lock) return [.. _directors]; }
    }

    public IDirector<IMessage> CreateDirector()
    {
        lock (_lock)
        {
            if (_directors.Count >= _options.Value.MaxDegreeOfParallelism)
                throw new InvalidOperationException("Max directors reached");

            _logger.LogInformation("Creating director");
            Director<IMessage> director = new(_options, _loggerFactory.CreateLogger<Director<IMessage>>());

            foreach (Type actorType in _actorRegistrationBuilder.ActorTypes)
            {
                director.RegisterActor(actorType.Name, () => (IActor<IMessage>)Activator.CreateInstance(actorType)!);
            }

            _directors.Add(director);
            return director;
        }
    }

    public void RemoveDirector(IDirector<IMessage> director)
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
        foreach (IDirector<IMessage> director in _directors)
        {
            director.ResumeActors();
        }
    }

    public WorkspaceStateExternal<IMessage> GetState()
    {
        DirectorStateExternal<IMessage>[] directorStates = [.. _directors.Select(x => x.GetState())];
        return new WorkspaceStateExternal<IMessage>(directorStates.Length, directorStates);
    }
}