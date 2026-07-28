using System.Collections.Concurrent;
using System.Linq.Expressions;
using YBotEngine.Data;
using YBotEngine.Runners.Abstractions;

namespace YBotEngine.Services;

public interface IScriptContextFactory
{
    IRunnerContext CreateContext(Type payloadType, object payload, IServiceProvider serviceProvider);
}

public class ScriptContextFactory : IScriptContextFactory
{
    private readonly ConcurrentDictionary<Type, Func<object, IServiceProvider, IRunnerContext>> _activatorCache = new();

    public IRunnerContext CreateContext(Type payloadType, object payload, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(payloadType);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var activator = _activatorCache.GetOrAdd(payloadType, CreateContextActivator);

        return activator(payload, serviceProvider);
    }

    private static Func<object, IServiceProvider, IRunnerContext> CreateContextActivator(Type payloadType)
    {
        var genericContextType = typeof(DiscordRoslynScriptContext<>).MakeGenericType(payloadType);

        var constructor = genericContextType.GetConstructor([payloadType, typeof(IServiceProvider)])
                          ?? throw new InvalidOperationException(
                              $"Could not find matching primary constructor on {genericContextType.Name}");

        var payloadParam = Expression.Parameter(typeof(object), "payload");
        var providerParam = Expression.Parameter(typeof(IServiceProvider), "provider");
        var castPayload = Expression.Convert(payloadParam, payloadType);
        var createNewInstance = Expression.New(constructor, castPayload, providerParam);
        var lambda = Expression.Lambda<Func<object, IServiceProvider, IRunnerContext>>(
            createNewInstance,
            payloadParam,
            providerParam
        );

        return lambda.Compile();
    }
}