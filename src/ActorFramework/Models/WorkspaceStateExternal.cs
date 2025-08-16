namespace ActorFramework.Models;

/// <summary>
/// Represents the state of the workspace. This is for external representation only.
/// </summary>
/// <param name="DirectorCount"></param>
/// <param name="DirectorStates"></param>
public record WorkspaceStateExternal(string Identifier, int DirectorCount, DirectorStateExternal[] DirectorStates);
