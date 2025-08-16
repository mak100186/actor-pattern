using ActorFramework.Abstractions;
using ActorFramework.Models;

namespace ActorFramework.Tests.Primitives;

public class MockActor : IActor
{
    public Task OnError(string actorId, IMessage message, Exception exception) => throw new NotImplementedException();

    public Task OnReceive(IMessage message, ActorContextExternal context, CancellationToken cancellationToken) => throw new NotImplementedException();
}
