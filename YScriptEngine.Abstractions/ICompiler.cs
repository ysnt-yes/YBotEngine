namespace YScriptEngine.Abstractions;

public interface ICompiler
{
    Task<IScript> CompileAsync(string scriptCode, Type contextType);
}

public static class CompilerExtensions
{
    public static Task<IScript> CompileAsync<TContext>(this ICompiler compiler, string scriptCode)
        where TContext : IScriptContext
    {
        return compiler.CompileAsync(scriptCode, typeof(TContext));
    }
}