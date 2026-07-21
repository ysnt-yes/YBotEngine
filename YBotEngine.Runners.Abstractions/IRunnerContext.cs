namespace YBotEngine.Runners.Abstractions;

public interface IRunnerContext
{
    IServiceProvider ServiceProvider { get; }
}

public interface IRunnerContext<out TData> : IRunnerContext
{
    TData Data { get; }
}