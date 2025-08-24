using ActorFramework.Abstractions;

namespace ActorFramework.Extensions;

public static class WorkspaceExtensions
{
    public static IDirector? GetDirectorForMessage(this IWorkspace workspace, IMessage message) => workspace.Directors
        .FirstOrDefault(x => x.ContainsActor(message));

    public static IDirector? GetFirstAvailableDirector(this IWorkspace workspace) => workspace.Directors
        .OrderBy(d => d.IsBusy()) // Prioritize not busy
        .FirstOrDefault(d => !d.IsBusy());

    public static IDirector GetLeastLoadedIdleDirector(this IWorkspace workspace) => workspace.Directors
        .OrderBy(d => d.TotalQueuedMessageCount) // least messages in mailbox
        .ThenBy(d => d.LastActive) // then by last active time
        .First();
}