using ActorFramework.Abstractions;
using ActorFramework.Configs;
using ActorFramework.Runtime.Orchestration;

using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace ActorFramework.Extensions;
public static class DependencyExtensions
{
    private const string ActorFrameworkSectionName = "ActorFrameworkOptions";

    public static IServiceCollection AddActorFramework(this IServiceCollection services, IConfiguration configuration, Action<ActorRegistrationBuilder> configure)
    {
        services.AddSingleton<Director<IMessage>>();

        var builder = new ActorRegistrationBuilder();
        configure(builder);

        foreach (var messageType in builder.MessageTypes)
        {
            var directorType = typeof(Director<>).MakeGenericType(messageType);

            services.AddSingleton(directorType);
        }

        services
            .AddOptions<ActorFrameworkOptions>()
            .Bind(configuration.GetSection(ActorFrameworkSectionName));

        return services;
    }
}

public class ActorRegistrationBuilder
{
    internal List<Type> MessageTypes { get; } = [];

    public ActorRegistrationBuilder AddMessage<T>()
        where T : class, IMessage
    {
        MessageTypes.Add(typeof(T));
        return this;
    }
}