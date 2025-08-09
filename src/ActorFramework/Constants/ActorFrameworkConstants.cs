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
}