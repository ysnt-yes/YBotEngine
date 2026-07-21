using System.Reflection;
using Microsoft.CodeAnalysis.Scripting;
using YBotEngine.Runners.Abstractions;

namespace YBotEngine.Runners.Roslyn;

public class RoslynRunner<TContext>(ScriptRunner<object> runDelegate) : IRunner where TContext : class, IRunnerContext
{
    public async Task ExecuteAsync(IRunnerContext context)
    {
        if (context is TContext concreteContext)
        {
            await runDelegate(concreteContext);
        }
        else
        {
            throw new ArgumentException($"Invalid context type. Expected {typeof(TContext).Name}, but received {context?.GetType().Name}");
        }
    }
}