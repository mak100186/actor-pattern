using ActorFramework.Abstractions;
using ActorFramework.Exceptions;
using ActorFramework.Runtime.Orchestration;

using Microsoft.AspNetCore.Mvc;

namespace ActorSystem.Controllers;

[ApiController]
[Route("[controller]")]
public class ActorController(ILogger<ActorController> logger, Director<TestMessage> director) : ControllerBase
{
    private static readonly Random _jitter = new Random();
    private const int MaxActorCount = 5;
    private const int MaxMessageCount = 5;

    private static bool shouldThrow = false;

    [HttpGet("SpawnActors")]
    public IActionResult SpawnActors()
    {
        for (var i = 0; i < MaxActorCount; i++)
        {
            try
            {
                director.RegisterActor($"actor{i}", () => new TestActor(logger, () => shouldThrow));
            }
            catch (ActorIdAlreadyRegisteredException ex)
            {
                logger.LogInformation(ex.Message);
            }
        }

        return Ok("Actors spawned");
    }

    [HttpGet("FlipHandler")]
    public IActionResult FlipHandler()
    {
        shouldThrow = !shouldThrow;
        return Ok(shouldThrow);
    }


    [HttpGet("ResumeActor")]
    public IActionResult ResumeActor(string actorId) => 
        Ok(director.ResumeActor(actorId));

    [HttpGet("SendMessages")]
    public async Task<IActionResult> SendMessages()
    {
        for (var i = 0; i < MaxActorCount; i++)
        {
            for (var j = 0; j < MaxMessageCount; j++)
            {
                await director.Send($"actor{i}", new TestMessage(_jitter.Next(100, 500)));
            }
        }

        return Ok("Messages sent.");
    }

    [HttpGet("MailboxStatuses")]
    public IActionResult MailboxStatuses()
    {
        var statuses = director.GetMailboxStatuses();
        return Ok(statuses);
    }
}

public record TestMessage(int Delay) : IMessage;

public class TestActor(ILogger logger, Func<bool> shouldThrow) : IActor<TestMessage>
{
    public async Task OnReceive(TestMessage message, ActorContext<TestMessage> context)
    {
        if (context.ActorId == "actor4" && shouldThrow())
        {
            throw new Exception("Testing actor pause on error");
        }

        var delayMs = message.Delay;

        logger.LogInformation("RX: {ActorId} {Delay} ms", context.ActorId, delayMs);

        // Simulate some work
        await Task.Delay(delayMs);

        logger.LogInformation("Processed: {ActorId} {Delay} ms", context.ActorId, delayMs);
    }
}