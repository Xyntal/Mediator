namespace Xyntal.NET.Mediator.Abstractions;

public interface ISender
{
	Task<TResponse> Send<TResponse>(ICommand<TResponse> request, CancellationToken cancellationToken = default);
	Task<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);
	IAsyncEnumerable<TResponse> Stream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);
}