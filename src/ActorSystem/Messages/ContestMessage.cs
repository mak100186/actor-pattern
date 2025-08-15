using ActorFramework.Abstractions;

namespace ActorSystem.Messages;

public record ContestMessage(string Key, string FeedProvider, string Name, DateTimeOffset Start, DateTimeOffset End, int Delay) : IMessage;
