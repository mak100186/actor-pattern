using ActorSystem.Messages;

using Bogus;

public class ContestMessageBuilder
{
    private readonly Faker faker = new();

    private string? _key;
    private string? _feedProvider;
    private string? _name;
    private DateTimeOffset? _start;
    private DateTimeOffset? _end;
    private int? _delay;

    public ContestMessageBuilder WithKey(string key)
    {
        _key = key;
        return this;
    }

    public ContestMessageBuilder WithFeedProvider(string feedProvider)
    {
        _feedProvider = feedProvider;
        return this;
    }

    public ContestMessageBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ContestMessageBuilder WithStart(DateTimeOffset start)
    {
        _start = start;
        return this;
    }

    public ContestMessageBuilder WithEnd(DateTimeOffset end)
    {
        _end = end;
        return this;
    }

    public ContestMessageBuilder WithDelay(int delay)
    {
        _delay = delay;
        return this;
    }

    public ContestMessage Build()
    {
        return new ContestMessage(
            Key: _key ?? faker.Random.Guid().ToString(),
            FeedProvider: _feedProvider ?? faker.Company.CompanyName(),
            Name: _name ?? faker.Commerce.ProductName(),
            Start: _start ?? faker.Date.FutureOffset(),
            End: _end ?? faker.Date.FutureOffset(),
            Delay: _delay ?? faker.Random.Int(100, 5000)
        );
    }
}
