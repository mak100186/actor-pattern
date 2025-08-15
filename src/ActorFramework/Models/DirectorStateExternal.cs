namespace ActorFramework.Models;

/// <summary>
/// The state of the director. This is an external representation for director state.
/// </summary>
/// <typeparam name="TMessage"></typeparam>
/// <param name="ActorCount"></param>
/// <param name="ActorStates"></param>
/// <param name="IsBusy"></param>
/// <param name="TimestampText"></param>
public record DirectorStateExternal(string Identifier, int ActorCount, int TotalMessages, ActorStateExternal[] ActorStates, bool IsBusy, string TimestampText);
