using YBotEngine.Runners.Abstractions;

namespace YBotEngine.Data;

public class DiscordRoslynScriptContext<TPayload>(TPayload payload, IServiceProvider serviceProvider) : IRunnerContext
{
    private IServiceProvider ServiceProvider { get; init; } = serviceProvider;
    public TPayload Data { get; init; } = payload;

    public T GetService<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }
}

public struct EmptyPayload {}