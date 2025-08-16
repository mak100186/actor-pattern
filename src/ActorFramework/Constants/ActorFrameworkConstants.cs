using ActorFramework.Configs;

namespace ActorFramework.Constants;

/// <summary>
/// Framework-wide constants.
/// </summary>
public static class ActorFrameworkConstants
{
    /// <summary>
    /// The time in seconds after which the idle director would be pruned to conserve resources.
    /// </summary>
    public const int DefaultDirectorIdleThresholdForPruningInSec = 30;

    /// <summary>
    /// The max number of directors inside a workspace. Each director is a thread that schedules actors on it.
    /// </summary>
    public const int DefaultMaxDegreeOfParallelism = 10;

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
    public const MailboxType DefaultMailboxType = MailboxType.ConcurrentQueue;

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
    public const string CommitFailedAsMessageWasNotAtHeadOfQueue = "Commit failed: message was not at head of queue.";
    public const string ActorFaultedAfterMaxRetriesPausing = "Actor '{ActorId}' faulted on message after max retries; rolling back; pausing";
    public const string ActorSkippingFailedMessage = "Actor '{ActorId}' skipping failed message and continuing";
    public const string ThreadRunningDirector = "Thread [{ThreadId}] - Director[{DirectorIdentifier}] - Actor[{ActorIdentifier}]";
    public const string ResumingActor = "Resuming actor '{ActorId}'";
    public const string RemovingDirector = "Removing director '{DirectorId}'";
    public const string ShuttingDownDirectorCancellingActors = "Shutting down Director, cancelling actors...";
    public const string ShuttingDownWorkspaceDisposingDirectors = "Shutting down workspace, disposing directors...";
    public const string DispatchLoopsCompletedDisposingMailboxes = "Dispatch loops completed, disposing mailboxes...";
    public const string EnqueueOpBlockedAsMailboxAtCapacity = "Producer blocked as Mailbox is at capacity.";
    public const string EnqueueOpDropNewest = "Dropping incoming message due to overflow policy: {@Message}";
    public const string EnqueueOpDropOldest = "Dropping oldest message due to overflow policy: {@Message}";
    public const string EnqueueOpMailboxFull = "Message not added as mailbox is full: {@Message}";

    // User-facing messages and formats
    public const string ActorNotFoundFormat = "Actor '{0}' not found.";
    public const string ActorAlreadyProcessing = "Actor already processing.";
    public const string ActorResumed = "Actor resumed.";
}