using System.Reflection;
using Microsoft.CodeAnalysis.Scripting;
using YScriptEngine.Abstractions;

namespace YScriptEngine.Roslyn;

public class RoslynScript(ScriptRunner<object> runDelegate) : IScript
{
    public async Task ExecuteAsync(IScriptContext context)
    {
        await runDelegate(context);
    }
}