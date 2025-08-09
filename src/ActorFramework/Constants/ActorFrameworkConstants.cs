using ActorFramework.Configs;

namespace ActorFramework.Constants;

/// <summary>
/// Framework-wide constants.
/// </summary>
public static class ActorFrameworkConstants
{
    /// <summary>
    /// Fallback mailbox capacity if no configuration is provided.
    /// </summary>
    public const int DefaultMailboxCapacity = 1000;

    /// <summary>
    /// By default, how to handle message overflow in the mailbox.
    /// </summary>

    public const OverflowPolicy DefaultOverflowPolicy = OverflowPolicy.BlockProducer;


    /// <summary>
    /// By default, the type of mailbox used for actors.
    /// </summary>
    public const MailboxType DefaultMailboxType = MailboxType.Bounded;

    /// <summary>
    /// The default number of retries if an exception occurs while processing a message.
    /// </summary>
    public const int DefaultRetryCountIfExceptionOccurs = 3;

    /// <summary>
    /// Default execution behavior when the configured amount of exception count is exceeded.
    /// </summary>
    public const bool DefaultExecutionBehaviorOnExceedingConfiguredAmountOfExceptionCount = true;

    // Logging templates used across the Actor Framework
    public const string ActorRetryingOnMessage = "Actor '{ActorId}' retry {Attempt}/{RetryCount} on message";
    public const string UnhandledExceptionInActorProcessingMessage = "Unhandled exception in actor {ActorId} processing message {MessageType}";
    public const string ActorFaultedAfterMaxRetriesPausing = "Actor '{ActorId}' faulted on message after max retries; pausing";
    public const string ActorSkippingFailedMessageContinuing = "Actor '{ActorId}' skipping failed message and continuing";
    public const string ResumingActor = "Resuming actor '{ActorId}'";
    public const string ShuttingDownDirectorCancellingActors = "Shutting down Director, cancelling actors...";
    public const string DispatchLoopsCompletedDisposingMailboxes = "Dispatch loops completed, disposing mailboxes...";

    // User-facing messages and formats
    public const string ActorNotFoundFormat = "Actor '{0}' not found.";
    public const string ActorAlreadyProcessing = "Actor already processing.";
    public const string ActorResumed = "Actor resumed.";
}