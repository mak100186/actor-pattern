using ActorFramework.Configs;
using ActorFramework.Events;
using ActorFramework.Extensions;
using ActorFramework.Runtime.Orchestration;
using ActorFramework.Tests.Primitives;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace ActorFramework.Tests.Contexts;

public class WorkspaceTestContext
{
    public Mock<IOptions<ActorFrameworkOptions>> MockOptions { get; } = new();
    public Mock<ILogger<Workspace>> MockLogger { get; } = new();
    public Mock<ILoggerFactory> MockLoggerFactory { get; } = new();
    public Mock<IServiceProvider> MockServiceProvider { get; } = new();
    public Mock<IEventBus> MockEventBus { get; } = new();
    public ActorFrameworkOptions Options { get; } = new();
    public Workspace SubjectUnderTest { get; private set; } = null!;

    public WorkspaceTestContext WithOptions(Action<ActorFrameworkOptions> optionsSetter)
    {
        optionsSetter?.Invoke(Options);

        MockOptions
            .Setup(m => m.Value)
            .Returns(Options);

        return this;
    }

    public void CreateSubject() => SubjectUnderTest = new(
        actorRegistrationBuilder: new ActorRegistrationBuilder().AddActor<MockActor, MockMessage>(),
        options: MockOptions.Object,
        logger: MockLogger.Object,
        loggerFactory: MockLoggerFactory.Object,
        serviceProvider: MockServiceProvider.Object,
        eventBus: MockEventBus.Object
    );

    public void VerifyEventPublished<TEvent>(Times times) where TEvent : class => MockEventBus.Verify(m => m.Publish(It.IsAny<TEvent>()), times);
}
