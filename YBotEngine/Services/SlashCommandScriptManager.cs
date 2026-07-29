using System.Collections.Concurrent;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using YBotEngine.Data;
using YBotEngine.Factories;
using YScriptEngine.Abstractions;

namespace YBotEngine.Services;

public class SlashCommandScriptManager<TCommandContext>(
    IServiceProvider serviceProvider,
    IScriptContextFactory contextFactory,
    IServiceScopeFactory scopeFactory) where TCommandContext : IApplicationCommandContext
{
    
    private readonly ConcurrentDictionary<string, IScript> _activeCommandRunners = new();
    public async Task CompileAndRegisterScriptAsync(string pathKey, string rawSource, string compilerType)
    {
        if (string.IsNullOrWhiteSpace(pathKey)) 
            throw new ArgumentException("Path key cannot be empty.");

        var compiler = serviceProvider.GetKeyedService<ICompiler>(compilerType.ToLower().Trim())
            ?? throw new NotSupportedException($"The script compiler type '{compilerType}' is not registered.");

        var contextType = typeof(RoslynScriptContext<TCommandContext>);
        var compiledRunner = await compiler.CompileAsync(rawSource, contextType);

        _activeCommandRunners[pathKey.ToLower().Trim()] = compiledRunner;
    }

    public void UnregisterCommandPath(string pathKey)
    {
        _activeCommandRunners.TryRemove(pathKey.ToLower().Trim(), out _);
    }

    public void ClearAll() => _activeCommandRunners.Clear();
    public async Task RouteCommandInteractionAsync(SlashCommandContext context)
    {
        var lookupPathKey = GetFlattenedCommandKey(context);

        if (_activeCommandRunners.TryGetValue(lookupPathKey, out var runner))
        {
            using var scope = scopeFactory.CreateScope();
            var scopedProvider = scope.ServiceProvider;
            
            var scriptContext = contextFactory.CreateContext(
                payloadType: typeof(SlashCommandContext),
                payload: context, 
                serviceProvider: scopedProvider
            );

            try
            {
                await runner.ExecuteAsync(scriptContext);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Script Runtime Crash] Route: {lookupPathKey} | Error: {ex.Message}");
                try { await context.Interaction.SendResponseAsync(InteractionCallback.Message($"Error: {ex.Message}")); }
                catch
                {
                    // ignored
                }
            }
        }
        else
        {
            await context.Interaction.SendResponseAsync(InteractionCallback.Message($"Routing Failure: No running script matches '/{lookupPathKey.Replace(':', ' ')}'."));
        }
    }

    public string GetFlattenedCommandKey(SlashCommandContext context)
    {
        var data = context.Interaction.Data;
        var pathSegments = new List<string> { data.Name.ToLower().Trim() };

        var currentOptions = context.Interaction.Data.Options;
        while (currentOptions is { Count: > 0 })
        {
            var firstOption = currentOptions[0];
            if (firstOption.Type is ApplicationCommandOptionType.SubCommand or ApplicationCommandOptionType.SubCommandGroup)
            {
                pathSegments.Add(firstOption.Name.ToLower().Trim());
                currentOptions = firstOption.Options;
            }
            else
            {
                break;
            }
        }
        return string.Join(':', pathSegments);
    }

    public bool HasActiveRunner(string lookupPathKey)
    {
        return _activeCommandRunners.TryGetValue(lookupPathKey, out _);
    }
}
