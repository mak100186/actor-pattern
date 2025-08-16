using System.Net;

using ActorFramework.Abstractions;
using ActorFramework.Models;
using ActorFramework.Runtime.Orchestration;

using ActorSystem.Messages;

using Bogus;

using Microsoft.AspNetCore.Mvc;

namespace ActorSystem.Controllers;

[ApiController]
[Route("[controller]")]
public class WorkspaceController(
    IWorkspace workspace,
    Faker<ContestMessage> contestFaker,
    Faker<PropositionMessage> propositionFaker,
    WorkspaceLoadBalancer workspaceLoadBalancer) : ControllerBase
{

    [HttpGet("SendMessages")]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> SendMessagesAsync(int countOfMessagesToGenerate)
    {
        for (var i = 0; i < countOfMessagesToGenerate; i++)
        {
            IMessage message = i % 2 == 0 ? contestFaker.Generate() : propositionFaker.Generate();

            await workspaceLoadBalancer.RouteAsync(message);
        }

        return Ok("Messages sent.");
    }

    [HttpGet("PruneWorkspace")]
    public IActionResult PruneWorkspace()
    {
        workspaceLoadBalancer.PruneIdleDirectors();
        return Ok();
    }

    [HttpGet("WorkspaceState")]
    [ProducesResponseType(typeof(IReadOnlyList<IReadOnlyDictionary<string, ActorStateExternal>>), (int)HttpStatusCode.OK)]
    public IActionResult WorkspaceState()
    {
        var statuses = workspace.GetState();
        return Ok(statuses);
    }
}
