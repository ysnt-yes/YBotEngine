using YBotEngine.Runners.Abstractions;

namespace YBotEngine.Services;

public class DiscordEventContext(object payload, IServiceProvider serviceProvider) : IRunnerContext
{
    public IServiceProvider ServiceProvider { get; } = serviceProvider;
    public object Data { get; } = payload;

    public T GetService<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }
}