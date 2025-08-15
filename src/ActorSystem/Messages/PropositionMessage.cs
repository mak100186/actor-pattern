using ActorFramework.Abstractions;

namespace ActorSystem.Messages;


public enum PropositionAvailability
{
    PreGame,
    InPlay
}

public record PropositionMessage(
    string Key,
    string ContestKey,
    string Name,
    PropositionAvailability PropositionAvailability,
    bool IsOpen,
    int Delay
    ) : IMessage;
