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

        IDirector director =
            workspace.GetFirstAvailableDirector()
            ?? workspace.CreateDirector()
            ?? workspace.GetLeastLoadedIdleDirector();

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

public static class WorkspaceExtensions
{
    public static IDirector? GetFirstAvailableDirector(this IWorkspace workspace)
    {
        return workspace.Directors
            .OrderBy(d => d.IsBusy()) // Prioritize not busy
            .FirstOrDefault(d => !d.IsBusy());
    }

    public static IDirector GetLeastLoadedIdleDirector(this IWorkspace workspace)
    {
        return workspace.Directors
            .OrderBy(d => d.TotalQueuedMessageCount) // least messages in mailbox
            .ThenBy(d => d.LastActive) // then by last active time
            .First();
    }
}