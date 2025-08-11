using System.Runtime.CompilerServices;

namespace Xyntal.NET.Mediator;

public class Mediator
	(
		IServiceProvider serviceProvider,
		IReadOnlyDictionary<Type, Func<IServiceProvider, object, CancellationToken, Task<object>>> factories,
		IReadOnlyDictionary<Type, Func<IServiceProvider, object, CancellationToken, Task>> notificationsFactories,
		IReadOnlyDictionary<Type, Func<IServiceProvider, object, CancellationToken, IAsyncEnumerable<object>>> streamsFactories
	) : IMediator
{
	public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
	{
		if (!factories.TryGetValue(request.GetType(), out var factory))
		{
			throw new InvalidOperationException($"No handler found for {request.GetType().Name}");
		}

		var response = await factory(serviceProvider, request, cancellationToken);
		return (TResponse)response!;
	}

	public async Task Publish(INotification request, CancellationToken cancellationToken = default)
	{
		if (!notificationsFactories.TryGetValue(request.GetType(), out var factory))
		{
			throw new InvalidOperationException($"No handler(s) found for {request.GetType().Name}");
		}

		await factory.Invoke(serviceProvider, request, cancellationToken);
	}

	public async IAsyncEnumerable<TResponse> Stream<TResponse>(IStreamRequest<TResponse> request, [EnumeratorCancellation]CancellationToken cancellationToken = default)
	{
		if (!streamsFactories.TryGetValue(request.GetType(), out var factory))
		{
			throw new InvalidOperationException($"No handler found for {request.GetType().Name}");
		}

		await foreach (var item in factory.Invoke(serviceProvider, request, cancellationToken).ConfigureAwait(false))
		{
			if (cancellationToken.IsCancellationRequested)
			{
				yield break;
			}

			yield return (TResponse)item!;
		}
	}
}