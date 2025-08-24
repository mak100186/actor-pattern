using ActorFramework.Models;

namespace ActorFramework.Abstractions;

public interface IDirector : IDisposable, IAsyncDisposable, IIdentifiable
{
    int TotalQueuedMessageCount { get; }
    DateTimeOffset LastActive { get; }
    void ResumeActors();
    DirectorStateExternal GetState();
    bool ContainsActor(IMessage message);
    int GetQueuedMessageCountForActor(IMessage message);
    ValueTask Send(IMessage message);
    bool IsBusy();
}
