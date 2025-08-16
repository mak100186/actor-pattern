using ActorFramework.Abstractions;
using ActorFramework.Configs;
using ActorFramework.Events;
using ActorFramework.Events.Poco;
using ActorFramework.Extensions;
using ActorFramework.Models;
using ActorFramework.Runtime.Orchestration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace ActorFramework.Tests;

public class MockMessage : IMessage { }
public class MockActor : IActor
{
    public Task OnError(string actorId, IMessage message, Exception exception)
    {
        throw new NotImplementedException();
    }

    public Task OnReceive(IMessage message, ActorContextExternal context, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
public class EventCapture<TEvent> where TEvent : class
{
    public TEvent? Event { get; private set; }

    public EventCapture<TEvent> SetupCapture(Mock<IEventBus> mock)
    {
        mock.Setup(m => m.Publish(It.IsAny<TEvent>()))
            .Callback<TEvent>(evt => Event = evt)
            .Verifiable();

        return this;
    }
}
public class WorkspaceTestContext
{
    public Mock<IOptions<ActorFrameworkOptions>> MockOptions { get; } = new();
    public Mock<ILogger<Workspace>> MockLogger { get; } = new();
    public Mock<ILoggerFactory> MockLoggerFactory { get; } = new();
    public Mock<IServiceProvider> MockServiceProvider { get; } = new();
    public Mock<IEventBus> MockEventBus { get; } = new();
    public ActorFrameworkOptions Options { get; } = new();
    public Workspace SubjectUnderTest { get; private set; }

    public WorkspaceTestContext WithOptions(Action<ActorFrameworkOptions> optionsSetter)
    {
        optionsSetter?.Invoke(Options);

        MockOptions
            .Setup(m => m.Value)
            .Returns(Options);

        return this;
    }

    public void CreateSubject()
    {
        SubjectUnderTest = new(
            actorRegistrationBuilder: new ActorRegistrationBuilder().AddActor<MockActor, MockMessage>(),
            options: MockOptions.Object,
            logger: MockLogger.Object,
            loggerFactory: MockLoggerFactory.Object,
            serviceProvider: MockServiceProvider.Object,
            eventBus: MockEventBus.Object
        );
    }

    public void VerifyEventPublished<TEvent>(Times times) where TEvent : class
    {
        MockEventBus.Verify(m => m.Publish(It.IsAny<TEvent>()), times);
    }
}
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
}
