using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.DependencyInjection;
using NetCord.Gateway;
using NetCord.Rest;

namespace YBotEngine.Extensions;

public static class ScriptOptionsExtension
{
    public static IServiceCollection AddDefaultScriptOptions(this IServiceCollection services)
    {
        services.AddSingleton<ScriptOptions>(sp =>
        {
            var coreAssemblyLocation = typeof(object).Assembly.Location;
            var runtimeDirectory = Path.GetDirectoryName(coreAssemblyLocation)!;

            var coreAssemblies = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(coreAssemblyLocation),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Runtime.dll")),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Collections.dll")),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Threading.Tasks.dll")),
                MetadataReference.CreateFromFile(Assembly.GetEntryAssembly()!.Location) 
            };

            var appDomainAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location));

            var netCordAssemblies = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(GatewayClient).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(RestClient).Assembly.Location)
            };

            var allReferences = coreAssemblies
                .Concat(appDomainAssemblies)
                .Concat(netCordAssemblies)
                .GroupBy(r => r is PortableExecutableReference pe ? pe.FilePath : r.ToString()) 
                .Select(g => g.First())
                .ToArray();

            string[] globalImports =
            [
                "System", "System.IO", "System.Text", "System.Text.Json", "System.Linq",
                "System.Collections.Generic", "System.Threading.Tasks", "NetCord",
                "NetCord.Gateway", "NetCord.Rest", "NetCord.Services", "YBotEngine.Services"
            ];

            return ScriptOptions.Default
                .WithReferences(allReferences)
                .WithImports(globalImports);
        });
        
        return services;
    }
}
