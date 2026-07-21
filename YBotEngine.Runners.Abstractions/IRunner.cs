namespace YBotEngine.Runners.Abstractions;

public interface IRunner
{
    Task ExecuteAsync(IRunnerContext context);
}
