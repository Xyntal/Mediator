namespace Xyntal.NET.Mediator;

public class Mediator
    (
        IServiceProvider serviceProvider,
        IReadOnlyDictionary<Type, Func<IServiceProvider, object, CancellationToken, Task<object>>> factories,
        IReadOnlyDictionary<Type, Func<IServiceProvider, object, CancellationToken, Task>> notificationsFactories
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







    public IAsyncEnumerable<TResponse> Stream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        Type requestType = request.GetType();
        Type responseType = typeof(TResponse);
        Type handlerType = typeof(IStreamRequestHandler<,>).MakeGenericType(requestType, responseType);

        object handler = serviceProvider.GetService(handlerType);
        var handleMethod = handlerType.GetMethod("Handle");

        return (IAsyncEnumerable<TResponse>)handleMethod!.Invoke(handler, [request, cancellationToken]);
    }
}