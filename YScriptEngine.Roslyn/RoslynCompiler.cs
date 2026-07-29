using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using YScriptEngine.Abstractions;

namespace YScriptEngine.Roslyn;

public class RoslynCompiler(ScriptOptions globalOptions, ILogger<RoslynCompiler> logger) : ICompiler
{
    public Task<IScript> CompileAsync(string scriptCode, Type contextType)
{
    try
    {
        var types = new List<Type>();
        if (contextType.IsGenericType)
        {
            types.AddRange(contextType.GetGenericArguments());
        }

        var assemblies = types.Select(t => t.Assembly).Append(contextType.Assembly).Distinct();
        
        var namespaces = types.Select(t => t.Namespace).Append(contextType.Namespace)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()!;

        var runtimeOptions = globalOptions
            .AddReferences(assemblies)
            .AddImports(namespaces!);

        var script = CSharpScript.Create(scriptCode, runtimeOptions, globalsType: contextType);
    
        var diagnostics = script.Compile();
    
        if (diagnostics.Length > 0)
        {
            foreach (var diagnostic in diagnostics)
            {
                switch (diagnostic.Severity)
                {
                    case DiagnosticSeverity.Info:
                        logger.LogInformation(diagnostic.GetMessage());
                        break;
                    case DiagnosticSeverity.Warning:
                        logger.LogWarning(diagnostic.GetMessage());
                        break;
                    case DiagnosticSeverity.Error:
                        logger.LogError(diagnostic.GetMessage());
                        break;
                    case DiagnosticSeverity.Hidden:
                        break;
                    default:
                        logger.LogDebug(diagnostic.GetMessage());
                        break;
                }
            }
            
            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Count > 0)
            {
                throw new CompilationErrorException("Failed to compile user script.", diagnostics);
            }
        }

        return Task.FromResult<IScript>(new RoslynScript(script.CreateDelegate()));
    }
    catch (Exception exception)
    {
        return Task.FromException<IScript>(exception);
    }
}

}