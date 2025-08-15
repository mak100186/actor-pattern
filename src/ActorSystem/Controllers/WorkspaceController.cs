using System.Net;

using ActorFramework.Abstractions;
using ActorFramework.Models;
using ActorFramework.Runtime.Orchestration;

using Bogus;

using Microsoft.AspNetCore.Mvc;

namespace ActorSystem.Controllers;

[ApiController]
[Route("[controller]")]
public class WorkspaceController(IWorkspace workspace, WorkspaceLoadBalancer workspaceLoadBalancer, ILogger<WorkspaceController> logger) : ControllerBase
{
    private static readonly Faker<ContestMessage> contestFaker = new Faker<ContestMessage>()
        .CustomInstantiator(f => new ContestMessage(
            Key: f.Random.Guid().ToString(),
            FeedProvider: f.Company.CompanyName(),
            Name: f.Commerce.ProductName(),
            Start: f.Date.FutureOffset(),
            End: f.Date.FutureOffset(),
            Delay: f.Random.Int(100, 5000)
        ));

    private static readonly Faker<PropositionMessage> propositionFaker = new Faker<PropositionMessage>()
        .CustomInstantiator(f => new PropositionMessage(
            Key: f.Random.Guid().ToString(),
            ContestKey: f.Random.Guid().ToString(),
            Name: f.Commerce.Department(),
            propositionAvailability: f.PickRandom<PropositionAvailability>(),
            IsOpen: f.Random.Bool(),
            Delay: f.Random.Int(100, 5000)
        ));

    [HttpGet("SendMessages")]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> SendMessages(int countOfMessagesToGenerate)
    {
        IMessage message;

        for (int i = 0; i < countOfMessagesToGenerate; i++)
        {
            if (i % 2 == 0)
                message = contestFaker.Generate();
            else
                message = propositionFaker.Generate();

            await workspaceLoadBalancer.RouteAsync(message);
        }

        return Ok("Messages sent.");
    }

    [HttpGet("ResumeActor")]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.OK)]
    public IActionResult ResumeActors()
    {
        workspace.Resume();
        return Ok();
    }

    [HttpGet("WorkspaceState")]
    [ProducesResponseType(typeof(IReadOnlyList<IReadOnlyDictionary<string, ActorStateExternal>>), (int)HttpStatusCode.OK)]
    public IActionResult WorkspaceState()
    {
        WorkspaceStateExternal statuses = workspace.GetState();
        return Ok(statuses);
    }
}

public record ContestMessage(
    string Key,
    string FeedProvider,
    string Name,
    DateTimeOffset Start,
    DateTimeOffset End,
    int Delay
    ) : IMessage;

public class ContestActor(ILogger<ContestActor> logger) : IActor
{
    public Task OnError(string actorId, IMessage message, Exception exception)
    {
        //actor id can be used to resume if needed
        logger.LogError(exception, "Error processing message {@Message} on actor [{ActorId}]", message, actorId);
        return Task.CompletedTask;
    }

    public async Task OnReceive(IMessage message, ActorContext context, CancellationToken cancellationToken)
    {
        if (message is ContestMessage contestMessage)
        {
            int delayMs = contestMessage.Delay;

            // Simulate some work
            await Task.Delay(delayMs, cancellationToken);

            logger.LogInformation("Processed: {ActorId} {Delay} ms", context.ActorId, delayMs);
        }
    }
}

public enum PropositionAvailability
{
    PreGame,
    InPlay
}
public record PropositionMessage(
    string Key,
    string ContestKey,
    string Name,
    PropositionAvailability propositionAvailability,
    bool IsOpen,
    int Delay
    ) : IMessage;

public class PropositionActor(ILogger<PropositionActor> logger) : IActor
{
    public Task OnError(string actorId, IMessage message, Exception exception)
    {
        //actor id can be used to resume if needed
        logger.LogError(exception, "Error processing message {@Message} on actor [{ActorId}]", message, actorId);
        return Task.CompletedTask;
    }

    public async Task OnReceive(IMessage message, ActorContext context, CancellationToken cancellationToken)
    {
        if (message is PropositionMessage propositionMessage)
        {
            int delayMs = propositionMessage.Delay;

            // Simulate some work
            await Task.Delay(delayMs, cancellationToken);

            logger.LogInformation("Processed: {ActorId} {Delay} ms", context.ActorId, delayMs);
        }
    }
}