using ActorFramework.Abstractions;
using ActorFramework.Configs;

using ActorWorkspace.Abstractions;
using ActorWorkspace.Runtime.Infrastructure;
using ActorWorkspace.Runtime.Orchestration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ActorWorkspace.Extensions;

public static class DependencyExtensions
{
    private const string ActorFrameworkSectionName = "ActorFrameworkOptions";

    public static IServiceCollection AddActorWorkspace(this IServiceCollection services, IConfiguration configuration, Action<ActorRegistrationBuilder> configure)
    {
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
    private List<Type> _actorTypes = [];
    private Dictionary<Type, Type> _messageToActorMap = [];

    public IReadOnlyDictionary<Type, Type> MessageToActorMap => _messageToActorMap;

    public IReadOnlyList<Type> ActorTypes => _actorTypes;

    public ActorRegistrationBuilder AddActor<TActor, TMessage>()
        where TActor : class, IActor<TMessage>
        where TMessage : class, IMessage
    {
        _actorTypes.Add(typeof(TActor));
        _messageToActorMap[typeof(TMessage)] = typeof(TActor);

        return this;
    }
}