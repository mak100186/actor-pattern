using ActorFramework.Abstractions;
using ActorFramework.Configs;
using ActorFramework.Extensions;

using Microsoft.Extensions.Options;

namespace ActorFramework.Runtime.Orchestration;

public class WorkspaceLoadBalancer(ActorRegistrationBuilder actorRegistrationBuilder, IWorkspace workspace, IOptions<ActorFrameworkOptions> options)
{
    public async Task RouteAsync(IMessage message)
    {
        Type messageType = message.GetType();

        if (!actorRegistrationBuilder.MessageToActorMap.TryGetValue(messageType, out Type? actorType))
            throw new InvalidOperationException($"No actor registered for message type {messageType.Name}");

        string actorId = actorType.Name; // or any unique string you use as ID

        IDirector? director = workspace.Directors
            .OrderBy(d => d.IsBusy()) // or by queue length
            .FirstOrDefault(d => !d.IsBusy());

        if (director == null)
        {
            if (workspace.Directors.Count < options.Value.MaxDegreeOfParallelism)
            {
                director = workspace.CreateDirector();
            }
            else
            {
                director = workspace.Directors.OrderBy(d => d.TotalQueuedMessageCount).ThenBy(d => d.LastActive).First();
            }
        }

        await director.Send(actorId, message);
    }

    public void PruneIdleDirectors()
    {
        DateTimeOffset idleCutoff = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(options.Value.DirectorIdleThresholdForPruning);

        foreach (IDirector? d in workspace.Directors
            .Where(d => d.LastActive < idleCutoff)
            .Skip(1) // keep at least one
            .ToList())
        {
            workspace.RemoveDirector(d);
        }
    }
}