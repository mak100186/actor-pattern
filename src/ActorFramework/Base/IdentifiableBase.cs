using ActorFramework.Abstractions;

namespace ActorFramework.Base;

public abstract class IdentifiableBase : IIdentifiable
{
    public string Identifier { get; } = Guid.CreateVersion7().ToString();
}
