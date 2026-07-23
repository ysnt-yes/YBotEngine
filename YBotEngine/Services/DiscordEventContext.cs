using YBotEngine.Runners.Abstractions;

namespace YBotEngine.Services;

public class DiscordEventContext<TPayload>(TPayload payload, IServiceProvider serviceProvider) : IRunnerContext
{
    private IServiceProvider ServiceProvider { get; init; } = serviceProvider;
    public TPayload Data { get; init; } = payload;

    public T GetService<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }
}

public struct EmptyPayload {}