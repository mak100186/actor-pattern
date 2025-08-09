using ActorFramework.Configs;

namespace ActorFramework.Exceptions;

public class MailboxFullException() : Exception("Mailbox is full");

public class OverflowPolicyNotHandledException(OverflowPolicy policy, string source) : Exception($"The OverflowPolicy[{policy}] is not handled in [{source}]");

public class MailboxTypeNotHandledException(MailboxType type, string source) : Exception($"The MailboxType[{type}] is not handled in [{source}].");

public class ActorIdAlreadyRegisteredException(string actorId) : Exception($"Actor '{actorId}' is already registered.");

public class ActorIdNotFoundException(string actorId) : Exception($"Actor '{actorId}' not found.");