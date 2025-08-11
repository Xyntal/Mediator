namespace Xyntal.NET.Mediator.Abstractions;

public interface INotificationHandler<in TNotification>
{
	Task Handle(TNotification request, CancellationToken cancellationToken = default);
}
