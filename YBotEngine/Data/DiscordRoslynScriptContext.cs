
using YScriptEngine.Abstractions;

namespace YBotEngine.Data;

public class RoslynScriptContext<TPayload>(TPayload payload, IServiceProvider serviceProvider) : IScriptContext
{
    private IServiceProvider ServiceProvider { get; } = serviceProvider;
    public TPayload Data { get; } = payload;

    public T GetService<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }
}

public readonly struct EmptyPayload {}