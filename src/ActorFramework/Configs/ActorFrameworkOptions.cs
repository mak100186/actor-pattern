using ActorFramework.Constants;

namespace ActorFramework.Configs;

/// <summary>
/// Holds configurable settings for the actor framework.
/// </summary>
public class ActorFrameworkOptions
{
    /// <summary>
    /// Maximum number of messages a mailbox can buffer.
    /// </summary>
    /// <remarks>The default value is defined by <see
    /// cref="ActorFrameworkConstants.DefaultMailboxCapacity"/>.</remarks>
    public int MailboxCapacity { get; set; } = ActorFrameworkConstants.DefaultMailboxCapacity;

    /// <summary>
    /// Gets or sets the policy that determines how the mailbox handles messages when it reaches its capacity. Works if the <see cref="MailboxType"/>  is set to <see cref="MailboxType.Bounded"/>.
    /// </summary>
    /// <remarks>The default value is defined by <see
    /// cref="ActorFrameworkConstants.DefaultOverflowPolicy"/>.</remarks>
    public OverflowPolicy MailboxOverflowPolicy { get; set; } = ActorFrameworkConstants.DefaultOverflowPolicy;

    /// <summary>
    /// The type of mailbox to use for actors.
    /// </summary>
    /// <remarks>The default value is defined by <see
    /// cref="ActorFrameworkConstants.DefaultMailboxType"/>.</remarks>
    public MailboxType MailboxType { get; set; } = ActorFrameworkConstants.DefaultMailboxType;

    /// <summary>
    /// The number of times to retry processing a message if an exception occurs.
    /// </summary>
    public int RetryCountIfExceptionOccurs { get; set; } = ActorFrameworkConstants.DefaultRetryCountIfExceptionOccurs;

    /// <summary>
    /// Gets or sets a value indicating whether the execution should stop when an unhandled exception occurs.
    /// </summary>
    public bool ShouldStopOnUnhandledException { get; set; } = ActorFrameworkConstants.DefaultExecutionBehaviorOnExceedingConfiguredAmountOfExceptionCount;
}