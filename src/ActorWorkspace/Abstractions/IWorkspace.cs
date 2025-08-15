using ActorFramework.Abstractions;

using ActorWorkspace.Models;

namespace ActorWorkspace.Abstractions;

public interface IWorkspace
{
    WorkspaceStateExternal<IMessage> GetState();
    IReadOnlyList<IDirector<IMessage>> Directors { get; }
    IDirector<IMessage> CreateDirector();
    void RemoveDirector(IDirector<IMessage> director);
    void Resume();
}
