using System.Runtime.CompilerServices;

namespace Xyntal.NET.Mediator;

internal static class InvokeHelper
{
    public static async ValueTask<object> InvokeCommandRequest<TRequest, TResponse>(IServiceProvider sp,
        object requestObj, CancellationToken ct) where TRequest : ICommand<TResponse>
    {
        TRequest request = (TRequest)requestObj;

        ICommandHandler<TRequest, TResponse> handler = sp.GetRequiredService<ICommandHandler<TRequest, TResponse>>();
        IEnumerable<ICommandPipelineBehavior<TRequest, TResponse>> behaviors =
            sp.GetServices<ICommandPipelineBehavior<TRequest, TResponse>>()
                .OrderBy(x => x.GetType().GetCustomAttribute<PipelineOrderAttribute>()?.Order ?? 0)
            ?? Enumerable.Empty<ICommandPipelineBehavior<TRequest, TResponse>>();

        // TODO: Add support of Pre-Handler hooks

        Func<ValueTask<TResponse>> handlerDelegate = () => handler.Handle(request, ct);

        foreach (var b in behaviors.Reverse())
        {
            Func<ValueTask<TResponse>> next = handlerDelegate;
            handlerDelegate = () => b.Handle(request, ct, next);
        }

        TResponse? response = await handlerDelegate().ConfigureAwait(false);

        // TODO: Add support of Post-Handler hooks

        //// After hooks
        //foreach (var b in behaviors)
        //    await b.AfterHandlerAsync(request, response, ct).ConfigureAwait(false);

        return (object)response!;
    }

    public static async Task<object> InvokeRequest<TRequest, TResponse>(IServiceProvider sp, object requestObj,
        CancellationToken ct) where TRequest : IQuery<TResponse>
    {
        TRequest request = (TRequest)requestObj;

        IQueryHandler<TRequest, TResponse> handler = sp.GetRequiredService<IQueryHandler<TRequest, TResponse>>();
        IEnumerable<IPipelineBehavior<TRequest, TResponse>> behaviors =
            sp.GetServices<IPipelineBehavior<TRequest, TResponse>>()
                .OrderBy(x => x.GetType().GetCustomAttribute<PipelineOrderAttribute>()?.Order ?? 0)
            ?? Enumerable.Empty<IPipelineBehavior<TRequest, TResponse>>();

        // TODO: Add support of Pre-Handler hooks

        Func<Task<TResponse>> handlerDelegate = () => handler.Handle(request, ct);

        foreach (var b in behaviors.Reverse())
        {
            Func<Task<TResponse>> next = handlerDelegate;
            handlerDelegate = () => b.Handle(request, ct, next);
        }

        TResponse? response = await handlerDelegate().ConfigureAwait(false);

        // TODO: Add support of Post-Handler hooks

        //// After hooks
        //foreach (var b in behaviors)
        //    await b.AfterHandlerAsync(request, response, ct).ConfigureAwait(false);

        return (object)response!;
    }

    public static Task InvokeNotification<TRequest>(IServiceProvider sp, object requestObj, CancellationToken ct)
        where TRequest : INotification
    {
        try
        {
            TRequest request = (TRequest)requestObj;
            IEnumerable<INotificationHandler<TRequest>> handlers = sp.GetServices<INotificationHandler<TRequest>>();

            var tasks = handlers.Select(handler => Task.Run(async () => { await handler.Handle(request, ct); }, ct))
                .ToArray();

            return Task.WhenAll(tasks);
        }
        catch (AggregateException ex)
        {
            throw ex.Flatten();
        }
    }


    public static async IAsyncEnumerable<object> InvokeStream<TRequest, TResponse>(IServiceProvider sp,
        object requestObj, [EnumeratorCancellation] CancellationToken ct) where TRequest : IStreamRequest<TResponse>
    {
        TRequest request = (TRequest)requestObj;
        IStreamRequestHandler<TRequest, TResponse> handler =
            sp.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>();
        Func<IAsyncEnumerable<TResponse>> handlerDelegate = () => handler.Handle(request, ct);

        await foreach (var result in handlerDelegate().ConfigureAwait(false))
        {
            if (ct.IsCancellationRequested)
            {
                yield break;
            }

            yield return (object)result!;
        }
    }
}