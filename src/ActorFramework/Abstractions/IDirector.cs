using ActorFramework.Models;

namespace ActorFramework.Abstractions;

public interface IDirector : IDisposable, IAsyncDisposable, IIdentifiable
{
    int TotalQueuedMessageCount { get; }
    DateTimeOffset LastActive { get; }
    void ResumeActors();
    DirectorStateExternal GetState();
    void RegisterActor(string actorId, Func<IActor> actorFactory);
    void RegisterLastActiveTimestamp();
    ValueTask Send(string actorId, IMessage message);
    bool IsBusy();
}
