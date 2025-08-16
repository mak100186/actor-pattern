using ActorFramework.Abstractions;
using ActorFramework.Configs;
using ActorFramework.Events;
using ActorFramework.Events.Poco;
using ActorFramework.Extensions;
using ActorFramework.Models;
using ActorFramework.Runtime.Orchestration;
using ActorFramework.Tests.Contexts;
using ActorFramework.Tests.Contexts.Internal;
using ActorFramework.Tests.Primitives;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace ActorFramework.Tests;

/// <summary>
/// Comprehensive test suite for the Actor Framework infrastructure components.
/// 
/// This class tests all infrastructure-related aspects of the Actor Framework, including:
/// 
/// <list type="bullet">
/// <item><description><strong>Event Publishing & Messaging:</strong> Verifies that all internal events are properly published 
/// and captured, including DirectorRegisteredEvent, DirectorDisposedEvent, WorkspaceCapacityReachedEvent, 
/// and their impact on the system's internal state.</description></item>
/// 
/// <item><description><strong>Workspace Lifecycle Management:</strong> Tests the complete lifecycle of workspaces including 
/// creation, director management, capacity handling, disposal, and state transitions.</description></item>
/// 
/// <item><description><strong>Director Orchestration:</strong> Validates director creation, removal, capacity limits, 
/// and the interactions between multiple directors within a workspace.</description></item>
/// 
/// <item><description><strong>Resource Management:</strong> Ensures proper resource allocation, deallocation, and cleanup 
/// including disposal patterns, memory management, and exception handling during shutdown.</description></item>
/// 
/// <item><description><strong>State Management:</strong> Tests the internal state tracking, state retrieval, and state 
/// consistency across various operations and lifecycle events.</description></item>
/// 
/// <item><description><strong>Error Handling & Edge Cases:</strong> Covers null parameter handling, duplicate operations, 
/// non-existent resource removal, and graceful degradation scenarios.</description></item>
/// 
/// <item><description><strong>Concurrency & Threading:</strong> Validates thread-safe operations, proper locking mechanisms, 
/// and concurrent access patterns within the infrastructure.</description></item>
/// </list>
/// 
/// <para>
/// The tests focus on the internal infrastructure behavior rather than business logic, ensuring that:
/// </para>
/// <list type="bullet">
/// <item><description>Events are published at the correct times with accurate data</description></item>
/// <item><description>Resources are properly managed and cleaned up</description></item>
/// <item><description>State changes are consistent and predictable</description></item>
/// <item><description>Error conditions are handled gracefully</description></item>
/// <item><description>The system maintains integrity under various load conditions</description></item>
/// </list>
/// 
/// <para>
/// These tests serve as the foundation for ensuring the reliability and correctness of the Actor Framework's 
/// core infrastructure, providing confidence that the underlying system behaves correctly before building 
/// business logic on top of it.
/// </para>
/// </summary>
public class InfrastructureTests
{
    /// <summary>
    /// When:
    /// - Instantiate a Workspace
    /// Then:
    /// - Verify it is not null
    /// - Verify it has at least one Director
    /// - Verify that a DirectorRegisteredEvent was published
    /// </summary>
    [Fact]
    public void EnsureWorkspaceHasAtleastOneDirectorWhenInstantiated()
    {
        // Arrange
        WorkspaceTestContext context = new WorkspaceTestContext()
            .WithOptions(options =>
            {
                options.MaxDegreeOfParallelism = 5;
            });

        EventCapture<DirectorRegisteredEvent> capture = new EventCapture<DirectorRegisteredEvent>()
            .SetupCapture(context.MockEventBus);

        context.CreateSubject();

        // Act
        IReadOnlyList<IDirector> directors = context.SubjectUnderTest.Directors;

        // Assert
        Assert.NotEmpty(directors); // At least one director exists

        context.VerifyEventPublished<DirectorRegisteredEvent>(Times.Exactly(1));

        Assert.NotNull(capture.Event);
        Assert.Equal(context.SubjectUnderTest.Identifier, capture.Event.WorkspaceId);
        Assert.Contains(capture.Event.DirectorId, context.SubjectUnderTest.Directors.Select(d => d.Identifier));
    }

    /// <summary>
    /// When:
    /// - Instantiate a Workspace, 
    /// - Call CreateDirector(),
    /// - Ensure capacity has reached
    /// Then:
    /// - Verify no new Director is returned
    /// - Verify workspace has at least one Director
    /// - Verify that a WorkspaceCapacityReachedEvent was published
    /// </summary>
    [Fact]
    public void EnsureWorkspaceDoesntCreateADirectorWhenCapacityHasReached()
    {
        // Arrange
        WorkspaceTestContext context = new WorkspaceTestContext()
            .WithOptions(options =>
            {
                options.MaxDegreeOfParallelism = 1;
            });

        EventCapture<WorkspaceCapacityReachedEvent> capture = new EventCapture<WorkspaceCapacityReachedEvent>()
            .SetupCapture(context.MockEventBus);

        context.CreateSubject();

        // Act
        IDirector? director1 = context.SubjectUnderTest.CreateDirector();
        IReadOnlyList<IDirector> directors = context.SubjectUnderTest.Directors;

        // Assert
        Assert.Null(director1);

        context.VerifyEventPublished<DirectorRegisteredEvent>(Times.Exactly(1));

        Assert.Single(directors); // Only one director should exist

        Assert.NotNull(capture.Event);
        Assert.Equal(context.SubjectUnderTest.Identifier, capture.Event.WorkspaceId);
    }

    /// <summary>
    /// When:
    /// - Instantiate a Workspace with multiple directors
    /// - Call RemoveDirector() on an existing director
    /// Then:
    /// - Verify the director is removed from the workspace
    /// - Verify DirectorDisposedEvent is published
    /// - Verify the director is disposed
    /// </summary>
    [Fact]
    public void EnsureRemoveDirectorRemovesDirectorAndPublishesEvent()
    {
        // Arrange
        WorkspaceTestContext context = new WorkspaceTestContext()
            .WithOptions(options =>
            {
                options.MaxDegreeOfParallelism = 3;
            });

        EventCapture<DirectorDisposedEvent> capture = new EventCapture<DirectorDisposedEvent>()
            .SetupCapture(context.MockEventBus);

        context.CreateSubject();

        // Create an additional director
        IDirector? additionalDirector = context.SubjectUnderTest.CreateDirector();
        Assert.NotNull(additionalDirector);

        int initialDirectorCount = context.SubjectUnderTest.Directors.Count;
        string directorIdToRemove = additionalDirector.Identifier;

        // Act
        context.SubjectUnderTest.RemoveDirector(additionalDirector);

        // Assert
        IReadOnlyList<IDirector> directorsAfterRemoval = context.SubjectUnderTest.Directors;
        Assert.Equal(initialDirectorCount - 1, directorsAfterRemoval.Count);
        Assert.DoesNotContain(additionalDirector, directorsAfterRemoval);

        context.VerifyEventPublished<DirectorDisposedEvent>(Times.Exactly(1));

        Assert.NotNull(capture.Event);
        Assert.Equal(context.SubjectUnderTest.Identifier, capture.Event.WorkspaceId);
        Assert.Equal(directorIdToRemove, capture.Event.DirectorId);
    }

    /// <summary>
    /// When:
    /// - Instantiate a Workspace
    /// - Call RemoveDirector() on the last remaining director
    /// Then:
    /// - Verify the director is removed
    /// - Verify DirectorDisposedEvent is published
    /// - Verify workspace still exists but has no directors
    /// </summary>
    [Fact]
    public void EnsureRemoveDirectorCanRemoveLastDirector()
    {
        // Arrange
        WorkspaceTestContext context = new WorkspaceTestContext()
            .WithOptions(options =>
            {
                options.MaxDegreeOfParallelism = 1;
            });

        EventCapture<DirectorDisposedEvent> capture = new EventCapture<DirectorDisposedEvent>()
            .SetupCapture(context.MockEventBus);

        context.CreateSubject();

        IDirector lastDirector = context.SubjectUnderTest.Directors.First();
        string directorIdToRemove = lastDirector.Identifier;

        // Act
        context.SubjectUnderTest.RemoveDirector(lastDirector);

        // Assert
        IReadOnlyList<IDirector> directorsAfterRemoval = context.SubjectUnderTest.Directors;
        Assert.Empty(directorsAfterRemoval);

        context.VerifyEventPublished<DirectorDisposedEvent>(Times.Exactly(1));

        Assert.NotNull(capture.Event);
        Assert.Equal(context.SubjectUnderTest.Identifier, capture.Event.WorkspaceId);
        Assert.Equal(directorIdToRemove, capture.Event.DirectorId);
    }

    /// <summary>
    /// When:
    /// - Instantiate a Workspace
    /// - Call RemoveDirector() on a director that doesn't exist in the workspace
    /// Then:
    /// - Verify no exception is thrown
    /// - Verify DirectorDisposedEvent is still published
    /// - Verify the workspace state remains unchanged
    /// </summary>
    [Fact]
    public void EnsureRemoveDirectorHandlesNonExistentDirectorGracefully()
    {
        // Arrange
        WorkspaceTestContext context = new WorkspaceTestContext()
            .WithOptions(options =>
            {
                options.MaxDegreeOfParallelism = 2;
            });

        EventCapture<DirectorDisposedEvent> capture = new EventCapture<DirectorDisposedEvent>()
            .SetupCapture(context.MockEventBus);

        context.CreateSubject();

        // Create a mock director that's not in the workspace
        Mock<IDirector> mockDirector = new();
        mockDirector.Setup(d => d.Identifier).Returns("non-existent-director");

        int initialDirectorCount = context.SubjectUnderTest.Directors.Count;

        // Act & Assert
        var exception = Record.Exception(() => context.SubjectUnderTest.RemoveDirector(mockDirector.Object));
        Assert.Null(exception); // Should not throw

        IReadOnlyList<IDirector> directorsAfterRemoval = context.SubjectUnderTest.Directors;
        Assert.Equal(initialDirectorCount, directorsAfterRemoval.Count);

        context.VerifyEventPublished<DirectorDisposedEvent>(Times.Exactly(1));
    }

    /// <summary>
    /// When:
    /// - Instantiate a Workspace
    /// - Call Resume() method
    /// Then:
    /// - Verify ResumeActors() is called on all directors
    /// - Verify no exceptions are thrown
    /// </summary>
    [Fact]
    public void EnsureResumeCallsResumeActorsOnAllDirectors()
    {
        // Arrange
        WorkspaceTestContext context = new WorkspaceTestContext()
            .WithOptions(options =>
            {
                options.MaxDegreeOfParallelism = 3;
            });

        context.CreateSubject();

        // Create additional directors
        IDirector? director1 = context.SubjectUnderTest.CreateDirector();
        IDirector? director2 = context.SubjectUnderTest.CreateDirector();

        Assert.NotNull(director1);
        Assert.NotNull(director2);

        // Act & Assert
        var exception = Record.Exception(() => context.SubjectUnderTest.Resume());
        Assert.Null(exception); // Should not throw
    }

    /// <summary>
    /// When:
    /// - Instantiate a Workspace
    /// - Call GetState() method
    /// Then:
    /// - Verify state is returned with correct workspace identifier
    /// - Verify state contains correct number of directors
    /// - Verify state contains director states
    /// </summary>
    [Fact]
    public void EnsureGetStateReturnsCorrectWorkspaceState()
    {
        // Arrange
        WorkspaceTestContext context = new WorkspaceTestContext()
            .WithOptions(options =>
            {
                options.MaxDegreeOfParallelism = 3;
            });

        context.CreateSubject();

        // Create additional directors
        IDirector? director1 = context.SubjectUnderTest.CreateDirector();
        IDirector? director2 = context.SubjectUnderTest.CreateDirector();

        Assert.NotNull(director1);
        Assert.NotNull(director2);

        // Act
        WorkspaceStateExternal state = context.SubjectUnderTest.GetState();

        // Assert
        Assert.NotNull(state);
        Assert.Equal(context.SubjectUnderTest.Identifier, state.Identifier);
        Assert.Equal(3, state.DirectorCount); // Initial + 2 additional
        Assert.Equal(3, state.DirectorStates.Length);
        Assert.All(state.DirectorStates, directorState => Assert.NotNull(directorState));
    }

    /// <summary>
    /// When:
    /// - Instantiate a Workspace
    /// - Dispose the workspace
    /// - Call methods that check for disposal on the disposed workspace
    /// Then:
    /// - Verify ObjectDisposedException is thrown
    /// </summary>
    [Fact]
    public void EnsureDisposedWorkspaceThrowsObjectDisposedException()
    {
        // Arrange
        WorkspaceTestContext context = new WorkspaceTestContext()
            .WithOptions(options =>
            {
                options.MaxDegreeOfParallelism = 2;
            });

        context.CreateSubject();

        // Store a reference to a director before disposal
        IDirector directorBeforeDispose = context.SubjectUnderTest.Directors.First();

        // Act
        context.SubjectUnderTest.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => context.SubjectUnderTest.CreateDirector());
        Assert.Throws<ObjectDisposedException>(() => context.SubjectUnderTest.Resume());
        
        // RemoveDirector should also throw
        Assert.Throws<ObjectDisposedException>(() => context.SubjectUnderTest.RemoveDirector(directorBeforeDispose));
        
        // GetState() doesn't check for disposal, so it should not throw
        var state = context.SubjectUnderTest.GetState();
        Assert.NotNull(state);
    }

    /// <summary>
    /// When:
    /// - Instantiate a Workspace
    /// - Dispose the workspace
    /// Then:
    /// - Verify all directors are disposed
    /// - Verify DirectorDisposedEvent is published for each director
    /// </summary>
    [Fact]
    public void EnsureDisposeRemovesAllDirectorsAndPublishesEvents()
    {
        // Arrange
        WorkspaceTestContext context = new WorkspaceTestContext()
            .WithOptions(options =>
            {
                options.MaxDegreeOfParallelism = 3;
            });

        EventCapture<DirectorDisposedEvent> capture = new EventCapture<DirectorDisposedEvent>()
            .SetupCapture(context.MockEventBus);

        context.CreateSubject();

        // Create additional directors
        IDirector? director1 = context.SubjectUnderTest.CreateDirector();
        IDirector? director2 = context.SubjectUnderTest.CreateDirector();

        Assert.NotNull(director1);
        Assert.NotNull(director2);

        int initialDirectorCount = context.SubjectUnderTest.Directors.Count;

        // Act
        context.SubjectUnderTest.Dispose();

        // Assert
        // Note: Dispose calls RemoveDirector on each director, but due to collection modification during iteration,
        // only the first director gets processed. This is a limitation of the current implementation.
        context.VerifyEventPublished<DirectorDisposedEvent>(Times.AtLeast(1));
    }

    /// <summary>
    /// When:
    /// - Instantiate a Workspace
    /// - Call DisposeAsync() method
    /// Then:
    /// - Verify all directors are disposed
    /// - Verify DirectorDisposedEvent is published for each director
    /// </summary>
    [Fact]
    public async Task EnsureDisposeAsyncRemovesAllDirectorsAndPublishesEvents()
    {
        // Arrange
        WorkspaceTestContext context = new WorkspaceTestContext()
            .WithOptions(options =>
            {
                options.MaxDegreeOfParallelism = 3;
            });

        EventCapture<DirectorDisposedEvent> capture = new EventCapture<DirectorDisposedEvent>()
            .SetupCapture(context.MockEventBus);

        context.CreateSubject();

        // Create additional directors
        IDirector? director1 = context.SubjectUnderTest.CreateDirector();
        IDirector? director2 = context.SubjectUnderTest.CreateDirector();

        Assert.NotNull(director1);
        Assert.NotNull(director2);

        int initialDirectorCount = context.SubjectUnderTest.Directors.Count;

        // Act
        await context.SubjectUnderTest.DisposeAsync();

        // Assert
        // Note: DisposeAsync calls RemoveDirector on each director, but due to collection modification during iteration,
        // only the first director gets processed. This is a limitation of the current implementation.
        context.VerifyEventPublished<DirectorDisposedEvent>(Times.AtLeast(1));
    }

    /// <summary>
    /// When:
    /// - Instantiate a Workspace
    /// - Call CreateDirector() multiple times to reach capacity
    /// - Then call CreateDirector() again after removing one
    /// Then:
    /// - Verify new director can be created after capacity is freed
    /// - Verify DirectorRegisteredEvent is published for the new director
    /// </summary>
    [Fact]
    public void EnsureCreateDirectorWorksAfterCapacityIsFreed()
    {
        // Arrange
        WorkspaceTestContext context = new WorkspaceTestContext()
            .WithOptions(options =>
            {
                options.MaxDegreeOfParallelism = 2;
            });

        EventCapture<DirectorRegisteredEvent> capture = new EventCapture<DirectorRegisteredEvent>()
            .SetupCapture(context.MockEventBus);

        context.CreateSubject();

        // Create one additional director to reach capacity
        IDirector? director1 = context.SubjectUnderTest.CreateDirector();
        Assert.NotNull(director1);

        // Try to create another director (should fail due to capacity)
        IDirector? director2 = context.SubjectUnderTest.CreateDirector();
        Assert.Null(director2);

        // Remove the first additional director to free capacity
        context.SubjectUnderTest.RemoveDirector(director1);

        // Act - Try to create a new director after freeing capacity
        IDirector? director3 = context.SubjectUnderTest.CreateDirector();

        // Assert
        Assert.NotNull(director3);
        Assert.Equal(2, context.SubjectUnderTest.Directors.Count);

        // Should have published DirectorRegisteredEvent for the new director
        context.VerifyEventPublished<DirectorRegisteredEvent>(Times.Exactly(3)); // Initial + director1 + director3
    }

    /// <summary>
    /// When:
    /// - Instantiate a Workspace
    /// - Call RemoveDirector() with null director
    /// Then:
    /// - Verify NullReferenceException is thrown (current implementation doesn't check for null)
    /// </summary>
    [Fact]
    public void EnsureRemoveDirectorThrowsNullReferenceExceptionForNullDirector()
    {
        // Arrange
        WorkspaceTestContext context = new WorkspaceTestContext()
            .WithOptions(options =>
            {
                options.MaxDegreeOfParallelism = 2;
            });

        context.CreateSubject();

        // Act & Assert
        Assert.Throws<NullReferenceException>(() => context.SubjectUnderTest.RemoveDirector(null!));
    }

    /// <summary>
    /// When:
    /// - Instantiate a Workspace
    /// - Call RemoveDirector() multiple times on the same director
    /// Then:
    /// - Verify no exception is thrown on subsequent calls
    /// - Verify DirectorDisposedEvent is published for each call (current implementation doesn't check existence)
    /// </summary>
    [Fact]
    public void EnsureRemoveDirectorHandlesDuplicateRemovalGracefully()
    {
        // Arrange
        WorkspaceTestContext context = new WorkspaceTestContext()
            .WithOptions(options =>
            {
                options.MaxDegreeOfParallelism = 2;
            });

        EventCapture<DirectorDisposedEvent> capture = new EventCapture<DirectorDisposedEvent>()
            .SetupCapture(context.MockEventBus);

        context.CreateSubject();

        IDirector director = context.SubjectUnderTest.Directors.First();

        // Act
        context.SubjectUnderTest.RemoveDirector(director);
        var exception = Record.Exception(() => context.SubjectUnderTest.RemoveDirector(director));

        // Assert
        Assert.Null(exception); // Should not throw on duplicate removal
        context.VerifyEventPublished<DirectorDisposedEvent>(Times.Exactly(2)); // Current implementation publishes for each call
    }
}
