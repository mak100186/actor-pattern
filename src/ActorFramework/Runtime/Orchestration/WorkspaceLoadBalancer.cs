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
        if (!actorRegistrationBuilder.MessageToActorMap.TryGetValue(messageType, out _))
        {
            throw new InvalidOperationException($"No actor registered for message type {messageType.Name}");
        }

        IDirector? director = workspace.GetDirectorForMessage(message);

        if (director != null)
        {
            if (director.GetQueuedMessageCountForActor(message) > options.Value.MailboxCapacity / 2)
            {
                director =
                    workspace.GetFirstAvailableDirector()
                    ?? workspace.CreateDirector()
                    ?? workspace.GetLeastLoadedIdleDirector();
            }

            await director.Send(message);
        }
        else
        {
            director =
                    workspace.GetFirstAvailableDirector()
                    ?? workspace.CreateDirector();

            if (director == null)
            {
                PruneIdleDirectors();
            }

            director =
                    workspace.GetFirstAvailableDirector()
                    ?? workspace.CreateDirector()
                    ?? workspace.GetLeastLoadedIdleDirector()
                    ?? throw new InvalidOperationException("No directors available to handle the message.");

            await director.Send(message);
        }
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