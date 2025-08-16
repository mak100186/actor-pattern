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
        WorkspaceStateExternal statuses = workspace.GetState();
        return Ok(statuses);
    }
}
