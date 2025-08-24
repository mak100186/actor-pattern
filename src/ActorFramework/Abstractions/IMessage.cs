namespace ActorFramework.Abstractions;

/// <summary>
/// Represents an immutable message that actors exchange.
/// </summary>
public interface IMessage
{
    string GetPartitionKey();
}
