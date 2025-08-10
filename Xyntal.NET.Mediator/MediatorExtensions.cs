namespace Xyntal.NET.Mediator;

public static class MediatorExtensions
{
    public static IServiceCollection AddMediator(this IServiceCollection services)
    {
        return AddMediator(services, []);
    }

    public static IServiceCollection AddMediator(this IServiceCollection services, params Type[] types)
    {
        ConcurrentDictionary<Type, Func<IServiceProvider, object, CancellationToken, Task<object>>> factories = [];
        ConcurrentDictionary<Type, Func<IServiceProvider, object, CancellationToken, Task>> notificationsFactory = [];

        Assembly[] assemblies = types.Length == 0 ? [Assembly.GetEntryAssembly()] : [.. types.Select(Assembly.GetAssembly)];

        services.RegisterPipelineBehaviors(assemblies);
        services.RegisterQueryHandlers(assemblies, ref factories);
        services.RegisterNotificationHandlers(assemblies, ref notificationsFactory);




        foreach (var assembly in assemblies)
        {
            services.RegisterStreamHandlers(assembly);
        }

        services.AddSingleton(factories);
        services.AddSingleton(notificationsFactory);

        services.AddSingleton<ISender, Mediator>();
        services.AddSingleton<IPublisher, Mediator>();
        services.AddSingleton<IMediator, Mediator>();

        return services;
    }

    private static IServiceCollection RegisterPipelineBehaviors(this IServiceCollection services, IEnumerable<Assembly> assembliesToScan)
    {
        var handlerTypes = assembliesToScan
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .SelectMany(imp => imp.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>))
                       .Select(i => new
                       {
                           Interafce = i,
                           Implementations = imp,
                           RequestType = i.GetGenericArguments()[0],
                           ResponseType = i.GetGenericArguments()[1]
                       }));

        foreach (var handler in handlerTypes)
        {
            services.AddTransient(handler.Interafce, handler.Implementations);
        }

        return services;
    }

    private static IServiceCollection RegisterQueryHandlers(this IServiceCollection services, IEnumerable<Assembly> assembliesToScan, ref ConcurrentDictionary<Type, Func<IServiceProvider, object, CancellationToken, Task<object>>> factories)
    {
        var handlerTypes = assembliesToScan
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .SelectMany(impl => impl.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                          .Select(i => new
                          {
                              Interface = i,
                              Implementations = impl,
                              RequestType = i.GetGenericArguments()[0],
                              ResponseType = i.GetGenericArguments()[1]
                          }))
            .ToList();


        foreach (var handler in handlerTypes)
        {
            MethodInfo helperMethod = typeof(PipelineHelper).GetMethod(nameof(PipelineHelper.InvokeRequest), BindingFlags.Static | BindingFlags.Public)!
                                                            .MakeGenericMethod(handler.RequestType, handler.ResponseType);

            ParameterExpression spParam = Expression.Parameter(typeof(IServiceProvider), "sp");
            ParameterExpression reqParam = Expression.Parameter(typeof(object), "request");
            ParameterExpression ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

            MethodCallExpression call = Expression.Call(helperMethod, spParam, reqParam, ctParam);

            var lambda = Expression.Lambda<Func<IServiceProvider, object, CancellationToken, Task<object>>>(call, spParam, reqParam, ctParam);
            Func<IServiceProvider, object, CancellationToken, Task<object>> compiled = lambda.Compile();

            factories[handler.RequestType] = compiled;

            services.AddTransient(handler.Interface, handler.Implementations);
        }

        services.AddSingleton<IReadOnlyDictionary<Type, Func<IServiceProvider, object, CancellationToken, Task<object>>>>(factories);
        return services;
    }

    private static IServiceCollection RegisterNotificationHandlers(this IServiceCollection services, IEnumerable<Assembly> assembliesToScan, ref ConcurrentDictionary<Type, Func<IServiceProvider, object, CancellationToken, Task>> notificationsFactory)
    {
        var handlerTypes = assembliesToScan
          .SelectMany(a => a.GetTypes())
          .Where(t => !t.IsAbstract && !t.IsInterface)
          .SelectMany(impl => impl.GetInterfaces()
          .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
                        .Select(i => new
                        {
                            Interface = i,
                            Implementations = impl,
                            RequestType = i.GetGenericArguments()[0]
                        }))
          .GroupBy(x => x.RequestType)
          .ToList();

        foreach (var handler in handlerTypes)
        {
            foreach (var item in handler.ToList())
            {
                services.AddTransient(item.Interface, item.Implementations);
            }

            MethodInfo helperMethod = typeof(PipelineHelper).GetMethod(nameof(PipelineHelper.InvokeNotification), BindingFlags.Static | BindingFlags.Public)!
                                                            .MakeGenericMethod(handler.Key);

            ParameterExpression spParam = Expression.Parameter(typeof(IServiceProvider), "sp");
            ParameterExpression reqParam = Expression.Parameter(typeof(object), "request");
            ParameterExpression ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

            MethodCallExpression call = Expression.Call(helperMethod, spParam, reqParam, ctParam);

            var lambda = Expression.Lambda<Func<IServiceProvider, object, CancellationToken, Task>>(call, spParam, reqParam, ctParam);
            Func<IServiceProvider, object, CancellationToken, Task> compiled = lambda.Compile();

            notificationsFactory[handler.Key] = compiled;
        }

        services.AddSingleton<IReadOnlyDictionary<Type, Func<IServiceProvider, object, CancellationToken, Task>>>(notificationsFactory);
        return services;
    }
















    private static IServiceCollection RegisterStreamHandlers(this IServiceCollection services, Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
                   .Where(t => t.IsClass && !t.IsAbstract)
                   .SelectMany(t => t.GetInterfaces()
                       .Where(i => i.IsGenericType &&
                                  i.GetGenericTypeDefinition() == typeof(IStreamRequestHandler<,>))
                       .Select(i => new
                       {
                           HandlerType = t,
                           RequestType = i.GetGenericArguments()[0],
                           ResponseType = i.GetGenericArguments()[1]
                       }));

        foreach (var handler in handlerTypes)
        {
            var interfaceType = typeof(IStreamRequestHandler<,>)
                .MakeGenericType(handler.RequestType, handler.ResponseType);

            services.AddTransient(interfaceType, handler.HandlerType);
        }

        return services;
    }
}