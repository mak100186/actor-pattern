using ActorFramework.Abstractions;
using ActorFramework.Models;

namespace ActorWorkspace.Models;

public record WorkspaceStateExternal<TMessage>(int DirectorCount, DirectorStateExternal<TMessage>[] DirectorStates)
    where TMessage : class, IMessage;
