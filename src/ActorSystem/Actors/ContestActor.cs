using ActorFramework.Abstractions;
using ActorFramework.Models;

using ActorSystem.Messages;

namespace ActorSystem.Actors;

public class ContestActor(ILogger<ContestActor> logger) : IActor
{
    public Task OnError(string actorId, IMessage message, Exception exception)
    {
        //actor id can be used to resume if needed
        logger.LogError(exception, "Error processing message {@Message} on actor [{ActorId}]", message, actorId);
        return Task.CompletedTask;
    }

    public async Task OnReceive(IMessage message, ActorContextExternal context, CancellationToken cancellationToken)
    {
        if (message is ContestMessage contestMessage)
        {
            int delayMs = contestMessage.Delay;

            logger.LogInformation("Received ContestMessage: {Key} {FeedProvider} {Name} {Start} {End} {Delay} ms",
                contestMessage.Key,
                contestMessage.FeedProvider,
                contestMessage.Name,
                contestMessage.Start,
                contestMessage.End,
                delayMs);

            while (delayMs > 0 && !cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("DirectorId: {DirectorId} - ActorId: {ActorId} - Processing ContestMessage running on Thread {ThreadId}", context.DirectorId, context.ActorId, Environment.CurrentManagedThreadId);

                await Task.Delay(100, cancellationToken);
                delayMs -= 100;
            }

            logger.LogInformation("Processed: {ActorId} {Delay} ms", context.ActorId, delayMs);
        }
    }
}
