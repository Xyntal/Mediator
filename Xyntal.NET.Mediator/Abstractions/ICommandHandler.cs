namespace Xyntal.NET.Mediator.Abstractions;

public interface ICommandHandler<in TRequest, TResponse> where TRequest : ICommand<TResponse>
{
    ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
}