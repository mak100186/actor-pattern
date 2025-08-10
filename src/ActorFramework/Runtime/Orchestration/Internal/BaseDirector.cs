using ActorFramework.Abstractions;
using ActorFramework.Configs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActorFramework.Runtime.Orchestration;

public abstract partial class BaseDirector<TMessage>(IOptions<ActorFrameworkOptions> options, ILogger<Director<TMessage>> logger)
    where TMessage : class, IMessage
{
    protected readonly ActorFrameworkOptions Options = options.Value ?? throw new ArgumentNullException(nameof(options));
    protected readonly ILogger<Director<TMessage>> Logger = logger ?? throw new ArgumentNullException(nameof(logger));
}