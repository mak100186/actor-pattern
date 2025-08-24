using ActorFramework.Abstractions;

namespace ActorFramework.Tests.Primitives;

public class MockMessage(string Key) : IMessage
{
    public string GetPartitionKey() => Key;
}
