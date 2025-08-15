using ActorFramework.Base;
using ActorFramework.Configs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActorFramework.Runtime.Orchestration.Internal;

public abstract partial class BaseDirector(IOptions<ActorFrameworkOptions> options, ILogger<Director> logger) : IdentifiableBase
{
    protected readonly ActorFrameworkOptions Options = options.Value ?? throw new ArgumentNullException(nameof(options));
    protected readonly ILogger<Director> Logger = logger ?? throw new ArgumentNullException(nameof(logger));
}