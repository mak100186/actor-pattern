using ActorFramework.Abstractions;
using ActorFramework.Models;

using ActorSystem.Messages;

namespace ActorSystem.Actors;

public class PropositionActor(ILogger<PropositionActor> logger) : IActor
{
    public Task OnError(string actorId, IMessage message, Exception exception)
    {
        //actor id can be used to resume if needed
        logger.LogError(exception, "Error processing message {@Message} on actor [{ActorId}]", message, actorId);
        return Task.CompletedTask;
    }

    public async Task OnReceive(IMessage message, ActorContextExternal context, CancellationToken cancellationToken)
    {
        if (message is PropositionMessage propositionMessage)
        {
            var delayMs = propositionMessage.Delay;

            logger.LogInformation("Received PropositionMessage: {Key} {ContestKey} {Name} {Availability} {IsOpen} {Delay} ms",
                propositionMessage.Key,
                propositionMessage.ContestKey,
                propositionMessage.Name,
                propositionMessage.PropositionAvailability,
                propositionMessage.IsOpen,
                delayMs);

            while (delayMs > 0 && !cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("DirectorId: {DirectorId} - ActorId: {ActorId} - Processing PropositionMessage {MessageKey} running on Thread {ThreadId}", context.DirectorId, context.ActorId, propositionMessage.Key, Environment.CurrentManagedThreadId);

                await Task.Delay(100, cancellationToken);
                delayMs -= 100;
            }

            logger.LogInformation("Processed: {ActorId} {Delay} ms", context.ActorId, propositionMessage.Delay);
        }
    }
}
