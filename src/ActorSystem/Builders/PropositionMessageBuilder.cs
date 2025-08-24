using ActorSystem.Messages;

using Bogus;

public class PropositionMessageBuilder
{
    private readonly Faker faker = new();

    private string? _key;
    private string? _contestKey;
    private string? _name;
    private PropositionAvailability? _availability;
    private bool? _isOpen;
    private int? _delay;

    public PropositionMessageBuilder WithKey(string key)
    {
        _key = key;
        return this;
    }

    public PropositionMessageBuilder WithContestKey(string contestKey)
    {
        _contestKey = contestKey;
        return this;
    }

    public PropositionMessageBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public PropositionMessageBuilder WithAvailability(PropositionAvailability availability)
    {
        _availability = availability;
        return this;
    }

    public PropositionMessageBuilder WithIsOpen(bool isOpen)
    {
        _isOpen = isOpen;
        return this;
    }

    public PropositionMessageBuilder WithDelay(int delay)
    {
        _delay = delay;
        return this;
    }

    public PropositionMessage Build()
    {
        return new PropositionMessage(
            Key: _key ?? faker.Random.Guid().ToString(),
            ContestKey: _contestKey ?? faker.Random.Guid().ToString(),
            Name: _name ?? faker.Commerce.Department(),
            PropositionAvailability: _availability ?? faker.PickRandom<PropositionAvailability>(),
            IsOpen: _isOpen ?? faker.Random.Bool(),
            Delay: _delay ?? faker.Random.Int(100, 5000)
        );
    }
}
