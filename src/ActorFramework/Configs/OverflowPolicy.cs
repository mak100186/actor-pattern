namespace ActorFramework.Configs;

public enum OverflowPolicy
{
    BlockProducer,   // Wait until space frees
    DropOldest,      // Evict oldest in queue
    DropNewest,      // Reject the incoming message
    FailFast         // Throw on overflow
}