namespace Xyntal.NET.Mediator;

internal static class PipelineHelper
{
    public static async Task<object> InvokeRequest<TRequest, TResponse>(IServiceProvider sp, object requestObj, CancellationToken ct) where TRequest : IRequest<TResponse>
    {
        TRequest request = (TRequest)requestObj;

        IRequestHandler<TRequest, TResponse> handler = sp.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        IEnumerable<IPipelineBehavior<TRequest, TResponse>> behaviors = sp.GetServices<IPipelineBehavior<TRequest, TResponse>>()
                                                                          .OrderBy(x => x.GetType().GetCustomAttribute<PipelineOrderAttribute>()?.Order ?? 0)
                                                                          ?? Enumerable.Empty<IPipelineBehavior<TRequest, TResponse>>();

        // TODO: Add support for Pre-Handler hooks

        Func<Task<TResponse>> handlerDelegate = () => handler.Handle(request, ct);

        foreach (var b in behaviors.Reverse())
        {
            Func<Task<TResponse>> next = handlerDelegate;
            handlerDelegate = () => b.Handle(request, ct, next);
        }

        TResponse? response = await handlerDelegate().ConfigureAwait(false);

        // TODO: Add support for Post-Handler hooks

        //// After hooks
        //foreach (var b in behaviors)
        //    await b.AfterHandlerAsync(request, response, ct).ConfigureAwait(false);

        return (object)response!;
    }

    public static Task InvokeNotification<TRequest>(IServiceProvider sp, object requestObj, CancellationToken ct) where TRequest : INotification
    {
        try
        {
            TRequest request = (TRequest)requestObj;
            IEnumerable<INotificationHandler<TRequest>> handlers = sp.GetServices<INotificationHandler<TRequest>>();

            var tasks = handlers.Select(handler => Task.Run(async () =>
               {
                   await handler.Handle(request, ct);
               }, ct)).ToArray();

            return Task.WhenAll(tasks);
        }
        catch (AggregateException ex)
        {
            throw ex.Flatten();
        }
    }
}
