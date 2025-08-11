using Xyntal.NET.Mediator.Models;

namespace Xyntal.NET.Mediator;

public static class MediatorExtensions
{
	private static ParameterExpression spParam = Expression.Parameter(typeof(IServiceProvider), "sp");
	private static ParameterExpression reqParam = Expression.Parameter(typeof(object), "request");
	private static ParameterExpression ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

	public static IServiceCollection AddMediator(this IServiceCollection services)
	{
		return AddMediator(services, []);
	}

	public static IServiceCollection AddMediator(this IServiceCollection services, params Type[] types)
	{
		Dictionary<Type, Func<IServiceProvider, object, CancellationToken, Task<object>>> factories = [];
		Dictionary<Type, Func<IServiceProvider, object, CancellationToken, Task>> notificationsFactories = [];
		Dictionary<Type, Func<IServiceProvider, object, CancellationToken, IAsyncEnumerable<object>>> streamFactories = [];

		Assembly[] assemblies = types.Length == 0 ? [Assembly.GetEntryAssembly()] : [.. types.Select(Assembly.GetAssembly)];

		services.RegisterPipelineBehaviors(assemblies);
		services.RegisterQueryHandlers(assemblies, ref factories);
		services.RegisterNotificationHandlers(assemblies, ref notificationsFactories);
		services.RegisterStreamHandlers(assemblies, ref streamFactories);

		services.AddSingleton<IReadOnlyDictionary<Type, Func<IServiceProvider, object, CancellationToken, Task<object>>>>(factories);
		services.AddSingleton<IReadOnlyDictionary<Type, Func<IServiceProvider, object, CancellationToken, Task>>>(notificationsFactories);
		services.AddSingleton<IReadOnlyDictionary<Type, Func<IServiceProvider, object, CancellationToken, IAsyncEnumerable<object>>>>(streamFactories);

		services.AddSingleton<ISender, Mediator>();
		services.AddSingleton<IPublisher, Mediator>();
		services.AddSingleton<IMediator, Mediator>();

		return services;
	}

	private static HandlerTypeInfo[] GetHandlerTypes(IEnumerable<Assembly> assembliesToScan, Type typeToAdd)
	{
		return assembliesToScan
			.SelectMany(a => a.GetTypes())
			.Where(t => !t.IsAbstract && !t.IsInterface)
			.SelectMany(imp => imp.GetInterfaces()
			.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeToAdd)
						.Select(i => new HandlerTypeInfo()
						{
							Interface = i,
							Implementations = imp,
							RequestType = i.GetGenericArguments()[0],
							ResponseType = i.GetGenericArguments()[1]
						}))
			.ToArray();
	}

	private static IServiceCollection RegisterPipelineBehaviors(this IServiceCollection services, IEnumerable<Assembly> assembliesToScan)
	{
		HandlerTypeInfo[] handlerTypes = GetHandlerTypes(assembliesToScan, typeof(IPipelineBehavior<,>));

		foreach (var handler in handlerTypes)
		{
			services.AddTransient(handler.Interface, handler.Implementations);
		}

		return services;
	}

	private static IServiceCollection RegisterQueryHandlers(this IServiceCollection services, IEnumerable<Assembly> assembliesToScan, ref Dictionary<Type, Func<IServiceProvider, object, CancellationToken, Task<object>>> factories)
	{
		HandlerTypeInfo[] handlerTypes = GetHandlerTypes(assembliesToScan, typeof(IRequestHandler<,>));

		foreach (var handler in handlerTypes)
		{
			MethodInfo helperMethod = typeof(InvokeHelper).GetMethod(nameof(InvokeHelper.InvokeRequest), BindingFlags.Static | BindingFlags.Public)!
															.MakeGenericMethod(handler.RequestType, handler.ResponseType);

			MethodCallExpression call = Expression.Call(helperMethod, spParam, reqParam, ctParam);

			var lambda = Expression.Lambda<Func<IServiceProvider, object, CancellationToken, Task<object>>>(call, spParam, reqParam, ctParam);
			Func<IServiceProvider, object, CancellationToken, Task<object>> compiled = lambda.Compile();

			factories[handler.RequestType] = compiled;

			services.AddTransient(handler.Interface, handler.Implementations);
		}

		return services;
	}

	private static IServiceCollection RegisterNotificationHandlers(this IServiceCollection services, IEnumerable<Assembly> assembliesToScan, ref Dictionary<Type, Func<IServiceProvider, object, CancellationToken, Task>> notificationsFactory)
	{
		HandlerTypeInfo[] handlerTypes = GetHandlerTypes(assembliesToScan, typeof(INotificationHandler<>));

		foreach (var handler in handlerTypes.GroupBy(x => x.RequestType))
		{
			foreach (var item in handler.ToList())
			{
				services.AddTransient(item.Interface, item.Implementations);
			}

			MethodInfo helperMethod = typeof(InvokeHelper).GetMethod(nameof(InvokeHelper.InvokeNotification), BindingFlags.Static | BindingFlags.Public)!
															.MakeGenericMethod(handler.Key);

			MethodCallExpression call = Expression.Call(helperMethod, spParam, reqParam, ctParam);

			var lambda = Expression.Lambda<Func<IServiceProvider, object, CancellationToken, Task>>(call, spParam, reqParam, ctParam);
			Func<IServiceProvider, object, CancellationToken, Task> compiled = lambda.Compile();

			notificationsFactory[handler.Key] = compiled;
		}

		return services;
	}

	private static IServiceCollection RegisterStreamHandlers(this IServiceCollection services, IEnumerable<Assembly> assembliesToScan, ref Dictionary<Type, Func<IServiceProvider, object, CancellationToken, IAsyncEnumerable<object>>> streamFactories)
	{
		HandlerTypeInfo[] handlerTypes = GetHandlerTypes(assembliesToScan, typeof(IStreamRequestHandler<,>));

		foreach (var handler in handlerTypes)
		{
			MethodInfo helperMethod = typeof(InvokeHelper).GetMethod(nameof(InvokeHelper.InvokeStream), BindingFlags.Static | BindingFlags.Public)!
															.MakeGenericMethod(handler.RequestType, handler.ResponseType);

			MethodCallExpression call = Expression.Call(helperMethod, spParam, reqParam, ctParam);

			var lambda = Expression.Lambda<Func<IServiceProvider, object, CancellationToken, IAsyncEnumerable<object>>>(call, spParam, reqParam, ctParam);
			Func<IServiceProvider, object, CancellationToken, IAsyncEnumerable<object>> compiled = lambda.Compile();

			streamFactories[handler.RequestType] = compiled;

			services.AddTransient(handler.Interface, handler.Implementations);
		}

		return services;
	}
}