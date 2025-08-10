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

    [HttpGet("ShouldThrowException")]
    public IActionResult ShouldThrowException()
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
        var errors = new List<string>();
        for (var i = 0; i < MaxActorCount; i++)
        {
            for (var j = 0; j < MaxMessageCount; j++)
            {
                try
                {
                    await director.Send($"actor{i}", new TestMessage(_jitter.Next(100, 500)));
                }
                catch (ActorIdNotFoundException ex)
                {
                    errors.Add(ex.Message);
                }
            }
        }

        if (errors.Count > 0)
        {
            return Ok($"Messages sent. Errors encountered {string.Join(',', errors)}");
        }

        return Ok("Messages sent.");
    }

    [HttpGet("WorkspaceState")]
    public IActionResult WorkspaceState()
    {
        var statuses = director.GetRegistryState();
        return Ok(statuses);
    }

    [HttpGet("ReleaseActors")]
    public IActionResult ReleaseActors()
    {
        var statuses = director.ReleaseActors(ShouldReleaseActors);

        return Ok(statuses);
    }

    private static bool ShouldReleaseActors(ActorContext<TestMessage> actorContext)
    {
        if (actorContext.IsPaused)
        {
            return false; // Do not release paused actors
        }

        if (actorContext.PendingMessagesCount > 0)
        {
            return false; // Do not release actors with pending messages
        }

        if (actorContext.HasReceivedMessageWithin(TimeSpan.FromMilliseconds(1500)))
        {
            return false; // Do not release actors that have received messages recently
        }

        return true; // Release actors that are not paused and have no pending messages
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

    /// <inheritdoc />
    public Task OnError(string actorId, TestMessage message, Exception exception)
    {
        //actor id can be used to resume if needed
        logger.LogError(exception, "Error processing message {@Message} on actor [{ActorId}]", message, actorId);
        return Task.CompletedTask;
    }
}