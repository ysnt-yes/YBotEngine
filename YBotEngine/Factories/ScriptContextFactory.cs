using System.Collections.Concurrent;
using YBotEngine.Data;
using YScriptEngine.Abstractions;

namespace YBotEngine.Factories;

public interface IScriptContextFactory
{
    IScriptContext CreateContext(Type payloadType, object payload, IServiceProvider serviceProvider);
}

public class ScriptContextFactory : IScriptContextFactory
{
    private readonly ConcurrentDictionary<Type, Func<object, IServiceProvider, IScriptContext>> _activatorCache = new();

    public IScriptContext CreateContext(Type payloadType, object payload, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(payloadType);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var activator = _activatorCache.GetOrAdd(payloadType, CreateContextActivator);

        return activator(payload, serviceProvider);
    }

    private static Func<object, IServiceProvider, IScriptContext> CreateContextActivator(Type payloadType)
    {
        var genericContextType = typeof(RoslynScriptContext<>).MakeGenericType(payloadType);
    
        var objectFactory = ActivatorUtilities.CreateFactory(genericContextType, [payloadType, typeof(IServiceProvider)]);

        return (payload, provider) => (IScriptContext)objectFactory(provider, [payload, provider]);
    }
}