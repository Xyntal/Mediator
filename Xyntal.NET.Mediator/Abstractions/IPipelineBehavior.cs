namespace Xyntal.NET.Mediator.Abstractions;

public interface IPipelineBehavior<TRequest, TResponse>
{
	Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, Func<Task<TResponse>> next);
}

