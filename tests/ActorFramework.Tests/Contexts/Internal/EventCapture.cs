using ActorFramework.Events;
using Moq;

namespace ActorFramework.Tests.Contexts.Internal;

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
