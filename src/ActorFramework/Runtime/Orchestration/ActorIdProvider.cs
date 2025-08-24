using ActorFramework.Abstractions;

namespace ActorFramework.Runtime.Orchestration;

public class ActorIdProvider
{
    public virtual string GetActorId(IMessage message) => $"{message.GetType()}|{message.GetPartitionKey()}";
}