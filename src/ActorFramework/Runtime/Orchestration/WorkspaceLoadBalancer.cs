using ActorFramework.Abstractions;
using ActorFramework.Configs;
using ActorFramework.Extensions;

using Microsoft.Extensions.Options;

namespace ActorFramework.Runtime.Orchestration;

public class WorkspaceLoadBalancer(ActorRegistrationBuilder actorRegistrationBuilder, IWorkspace workspace, IOptions<ActorFrameworkOptions> options)
{
    public async Task RouteAsync(IMessage message)
    {
        var messageType = message.GetType();

        if (!actorRegistrationBuilder.MessageToActorMap.TryGetValue(messageType, out var actorType))
        {
            throw new InvalidOperationException($"No actor registered for message type {messageType.Name}");
        }

        var actorId = actorType.Name; // or any unique string you use as ID

        var director =
            workspace.GetFirstAvailableDirector()
            ?? workspace.CreateDirector()
            ?? workspace.GetLeastLoadedIdleDirector();

        await director.Send(actorId, message);
    }

    public void PruneIdleDirectors()
    {
        var idleCutoff = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(options.Value.DirectorIdleThresholdForPruning);

        foreach (var d in workspace.Directors
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
    public static IDirector? GetFirstAvailableDirector(this IWorkspace workspace) => workspace.Directors
        .OrderBy(d => d.IsBusy()) // Prioritize not busy
        .FirstOrDefault(d => !d.IsBusy());

    public static IDirector GetLeastLoadedIdleDirector(this IWorkspace workspace) => workspace.Directors
        .OrderBy(d => d.TotalQueuedMessageCount) // least messages in mailbox
        .ThenBy(d => d.LastActive) // then by last active time
        .First();
}