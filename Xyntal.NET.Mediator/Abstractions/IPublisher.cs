namespace Xyntal.NET.Mediator.Abstractions;

public interface IPublisher
{
    Task Publish(INotification request, CancellationToken cancellationToken = default);
}
