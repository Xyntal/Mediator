namespace Xyntal.NET.Mediator.Abstractions;

public interface IQueryHandler<in TRequest, TResponse> where TRequest : IQuery<TResponse>
{
	Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
}