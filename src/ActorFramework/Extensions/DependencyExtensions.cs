using ActorFramework.Abstractions;
using ActorFramework.Configs;
using ActorFramework.Events;
using ActorFramework.Runtime.Orchestration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ActorFramework.Extensions;

public static class DependencyExtensions
{
    private const string ActorFrameworkSectionName = "ActorFrameworkOptions";

    public static IServiceCollection AddActorFramework(this IServiceCollection services, IConfiguration configuration, Action<ActorRegistrationBuilder> configure)
    {
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddSingleton<IWorkspace, Workspace>();
        services.AddSingleton<WorkspaceLoadBalancer>();

        ActorRegistrationBuilder builder = new();
        configure(builder);

        services.AddSingleton(builder);

        services
            .AddOptions<ActorFrameworkOptions>()
            .Bind(configuration.GetSection(ActorFrameworkSectionName));

        return services;
    }
}

public class ActorRegistrationBuilder
{
    private readonly List<Type> _actorTypes = [];
    private readonly Dictionary<Type, Type> _messageToActorMap = [];

    public IReadOnlyDictionary<Type, Type> MessageToActorMap => _messageToActorMap;

    public IReadOnlyList<Type> ActorTypes => _actorTypes;

    public ActorRegistrationBuilder AddActor<TActor, TMessage>()
        where TActor : class, IActor
        where TMessage : class, IMessage
    {
        _actorTypes.Add(typeof(TActor));
        _messageToActorMap[typeof(TMessage)] = typeof(TActor);

        return this;
    }
}