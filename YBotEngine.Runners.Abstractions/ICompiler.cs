namespace YBotEngine.Runners.Abstractions;

public interface ICompiler
{
    Task<IRunner> CompileAsync(string scriptCode, Type contextType);
}

public static class CompilerExtensions
{
    public static Task<IRunner> CompileAsync<TContext>(this ICompiler compiler, string scriptCode)
        where TContext : IRunnerContext
    {
        return compiler.CompileAsync(scriptCode, typeof(TContext));
    }
}