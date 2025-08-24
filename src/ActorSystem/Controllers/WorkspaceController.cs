using System.Net;

using ActorFramework.Abstractions;
using ActorFramework.Models;
using ActorFramework.Runtime.Orchestration;

using Microsoft.AspNetCore.Mvc;

namespace ActorSystem.Controllers;

[ApiController]
[Route("[controller]")]
public class WorkspaceController(
    IWorkspace workspace,
    ContestMessageBuilder contestFaker,
    PropositionMessageBuilder propositionFaker,
    WorkspaceLoadBalancer workspaceLoadBalancer) : ControllerBase
{
    [HttpGet("SendContestMessage")]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> SendContestMessageAsync(string key)
    {
        var message = contestFaker.WithKey(key).Build();

        await workspaceLoadBalancer.RouteAsync(message);

        return Ok("Messages sent.");
    }

    [HttpGet("SendPropositionMessage")]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> SendPropositionMessageAsync(string key)
    {
        var message = propositionFaker.WithKey(key).Build();

        await workspaceLoadBalancer.RouteAsync(message);

        return Ok("Messages sent.");
    }

    [HttpGet("SendMessages")]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> SendMessagesAsync(int countOfMessagesToGenerate)
    {
        for (var i = 0; i < countOfMessagesToGenerate; i++)
        {
            IMessage message = i % 2 == 0 ? contestFaker.Build() : propositionFaker.Build();

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
