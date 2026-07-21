using System.Reflection;
using Microsoft.CodeAnalysis.Scripting;
using YBotEngine.Runners.Abstractions;

namespace YBotEngine.Runners.Roslyn;

public class RoslynRunner(ScriptRunner<object> runDelegate) : IRunner
{
    public async Task ExecuteAsync(IRunnerContext context)
    {
        await runDelegate(context);
    }
}