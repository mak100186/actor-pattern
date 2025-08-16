using ActorFramework.Models;

namespace ActorFramework.Abstractions;

public interface IWorkspace
{
    WorkspaceStateExternal GetState();
    IReadOnlyList<IDirector> Directors { get; }
    IDirector? CreateDirector();
    void RemoveDirector(IDirector director);
    void Resume();
}
