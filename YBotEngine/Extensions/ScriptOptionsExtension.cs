using Microsoft.CodeAnalysis.Scripting;
using NetCord.Gateway;
using NetCord.Rest;
using YBotEngine.Runners.Roslyn;

namespace YBotEngine.Extensions;

public static class ScriptOptionsExtension
{
    public static IServiceCollection AddDefaultScriptOptions(this IServiceCollection services)
    {

        services.AddSingleton<ScriptOptions>(sp =>
        {
            var coreAssemblies = new[]
            {
                typeof(object).Assembly,
                typeof(Console).Assembly,
                typeof(Enumerable).Assembly,
                typeof(List<>).Assembly,
                typeof(System.Text.Json.JsonSerializer).Assembly,
                typeof(Program).Assembly
            };

            var serviceAssemblies = services
                .Select(s => s.ServiceType.Assembly)
                .Distinct();

            var netCordAssemblies = new[]
            {
                typeof(GatewayClient).Assembly,
                typeof(RestClient).Assembly
                
            };

            var allAssemblies = coreAssemblies
                .Concat(serviceAssemblies)
                .Concat(netCordAssemblies)
                .Distinct()
                .ToArray();

            var globalImports = new[]
            {
                "System",
                "System.IO",
                "System.Text",
                "System.Text.Json",
                "System.Linq",
                "System.Collections.Generic",
                "System.Threading.Tasks",
                "NetCord",
                "NetCord.Gateway",
                "NetCord.Rest",
                "NetCord.Services"
            };

            return ScriptOptions.Default
                .WithReferences(allAssemblies)
                .WithImports(globalImports);
        });
        
        return services;
    }
}