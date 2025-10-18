namespace Xyntal.NET.Mediator.Abstractions;

public interface ICommandPipelineBehavior<in TRequest, TResponse>
{
    ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken, Func<ValueTask<TResponse>> next);
}