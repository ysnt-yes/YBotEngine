using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using YBotEngine.Data;
using YBotEngine.Runners.Abstractions;

namespace YBotEngine.Services;


public class UserCommandManager
{
    private readonly IServiceProvider _provider;
    private readonly GatewayClient _client;
    private readonly ApplicationCommandService<ApplicationCommandContext> _commandService;
    private readonly ILogger<UserCommandManager> _logger;

    private readonly ConcurrentDictionary<string, IRunner> _executionScripts = new();
    private readonly ConcurrentDictionary<string, byte> _establishedRootRoutes = new();

    public UserCommandManager(
        IServiceProvider provider, 
        GatewayClient client,
        ApplicationCommandService<ApplicationCommandContext> commandService,
        ILogger<UserCommandManager> logger)
    {
        _provider = provider;
        _client = client;
        _commandService = commandService;
        _logger = logger;

        _client.InteractionCreate += HandleInteractionAsync;
    }

    public async Task AttachScriptAsync(string triggerKey, string scriptBody, CompilerType compilerType)
    {
        var compiler = _provider.GetRequiredKeyedService<ICompiler>(compilerType);
        var contextType = typeof(DiscordRoslynScriptContext<ApplicationCommandContext>);
        
        _executionScripts[triggerKey] = await compiler.CompileAsync(scriptBody, contextType);
        _logger.LogInformation("Hot-Swap successful for script path: /{Path}", triggerKey.Replace(":", " "));
    }

    public async Task SyncCommandTreeAsync(string rootName, List<UserScript> associatedScripts, ulong? guildId)
    {
        EnsureProxyRoutesMounted(rootName, associatedScripts);

        var rootScript = associatedScripts.FirstOrDefault(s => s.TriggerKey == rootName);
        var optionsProperties = new List<ApplicationCommandOptionProperties>();

        // Process Subcommand Groups (2 Deep: e.g. "database:scripts:count")
        var groupScripts = associatedScripts
            .Where(s => s.TriggerKey.Split(':').Length == 3)
            .GroupBy(s => s.TriggerKey.Split(':')[1]); 

        foreach (var group in groupScripts)
        {
            var subOptions = new List<ApplicationCommandOptionProperties>();
            foreach (var subScript in group)
            {
                var segments = subScript.TriggerKey.Split(':');
                subOptions.Add(new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, segments[2], subScript.Description ?? "Dynamic Subcommand")
                {
                    Options = subScript.Options.Select(o => new ApplicationCommandOptionProperties(o.Type, o.Name, o.Description) { Required = o.IsRequired }).ToList()
                });
            }

            optionsProperties.Add(new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommandGroup, group.Key, $"Group {group.Key}")
            {
                Options = subOptions
            });
        }

        var directSubcommands = associatedScripts.Where(s => s.TriggerKey.Split(':').Length == 2);
        foreach (var subScript in directSubcommands)
        {
            var segments = subScript.TriggerKey.Split(':');
            optionsProperties.Add(new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, segments[1], subScript.Description ?? "Dynamic Subcommand")
            {
                Options = subScript.Options.Select(o => new ApplicationCommandOptionProperties(o.Type, o.Name, o.Description) { Required = o.IsRequired }).ToList()
            });
        }

        if (rootScript != null && optionsProperties.Count == 0)
        {
            optionsProperties.AddRange(rootScript.Options.Select(o => new ApplicationCommandOptionProperties(o.Type, o.Name, o.Description) { Required = o.IsRequired }));
        }

        var rootDescription = rootScript?.Description ?? $"Dynamic command bundle for {rootName}";
        
        SlashCommandProperties finalProperties = new(rootName, rootDescription)
        {
            Options = optionsProperties,
            DefaultGuildPermissions = rootScript?.RequiredGuildPermissions
        };

        var appId = _client.Id;
        if (guildId.HasValue)
        {
            await _client.Rest.CreateGuildApplicationCommandAsync(appId, (ulong)guildId, finalProperties);
        }
        else
        {
            await _client.Rest.CreateGlobalApplicationCommandAsync(appId, finalProperties);
        }
        _logger.LogInformation("Pushed dynamic structure tree layout for root: /{RootName} to Discord.", rootName);
    }

    private void EnsureProxyRoutesMounted(string rootName, List<UserScript> associatedScripts)
{
    if (!_establishedRootRoutes.TryAdd(rootName, 1)) return; 

    var rootScript = associatedScripts.FirstOrDefault(s => s.TriggerKey == rootName);
    var rootDescription = rootScript?.Description ?? "Dynamic tracking group";

    // Level 0 (No) Nested (e.g., "/ping")
    if (associatedScripts.Count == 1 && associatedScripts[0].TriggerKey == rootName)
    {
        Func<ApplicationCommandContext, Task> executionProxyHandler = async (context) => 
            await ExecuteScriptProxyAsync(rootName, context);

        var builder = new SlashCommandBuilder(rootName, rootDescription, (Delegate)executionProxyHandler);
        _commandService.AddSlashCommand(builder); 
        return;
    }

    var rootGroupBuilder = new SlashCommandGroupBuilder(rootName, rootDescription);

    // Level 1 Nested (e.g., "system:status")
    var level1Subs = associatedScripts.Where(s => s.TriggerKey.Split(':').Length == 2);
    
    foreach (var subScript in level1Subs)
    {
        var currentPath = subScript.TriggerKey;
        var segments = currentPath.Split(':');

        Func<ApplicationCommandContext, Task> subProxy = async (context) => 
            await ExecuteScriptProxyAsync(currentPath, context);

        rootGroupBuilder.AddSubCommand(segments[1], subScript.Description ?? "Dynamic Subcommand", (Delegate)subProxy);
    }

    // Level 2 Nested (e.g., "database:scripts:count")
    var level2Groups = associatedScripts
        .Where(s => s.TriggerKey.Split(':').Length == 3)
        .GroupBy(s => s.TriggerKey.Split(':')[1]);

    foreach (var group in level2Groups)
    {
        rootGroupBuilder.AddSubCommandGroup(group.Key, $"Group context {group.Key}", builder =>
        {
            foreach (var subScript in group)
            {
                var currentPath = subScript.TriggerKey;
                var segments = currentPath.Split(':');

                Func<ApplicationCommandContext, Task> subProxy = async (context) => 
                    await ExecuteScriptProxyAsync(currentPath, context);
            
                builder.AddSubCommand(segments[2], subScript.Description ?? "Dynamic Subcommand", (Delegate)subProxy);
            }
        });
    }
    
    _commandService.AddSlashCommandGroup(rootGroupBuilder);
}


    private async Task ExecuteScriptProxyAsync(string pathKey, ApplicationCommandContext context)
    {
        var startTime = Environment.TickCount64;

        if (!_executionScripts.TryGetValue(pathKey, out var runner))
        {
            await context.Interaction.SendResponseAsync(InteractionCallback.Message("This runtime logic block is currently recompiling."));
            return;
        }

        try
        {
            using var scope = _provider.CreateScope();
            var scriptGlobals = new DiscordRoslynScriptContext<ApplicationCommandContext>(context, scope.ServiceProvider);
            
            await runner.ExecuteAsync(scriptGlobals);

            var elapsedMs = Environment.TickCount64 - startTime;
            _logger.LogInformation("Script route Executed successfully: /{Path} in {ElapsedMs}ms", pathKey.Replace(":", " "), elapsedMs);
        }
        catch (Exception ex)
        {
            var elapsedMs = Environment.TickCount64 - startTime;
            _logger.LogError(ex, "Script route /{Path} threw an exception after {ElapsedMs}ms!", pathKey.Replace(":", " "), elapsedMs);
            await context.Interaction.SendResponseAsync(InteractionCallback.Message("An unhandled exception occurred within the custom script execution stack."));
        }
    }

    private ValueTask HandleInteractionAsync(Interaction interaction)
    {
        if (interaction is SlashCommandInteraction slashCommand)
        {
            _ = Task.Run(async () =>
            {
                var context = new ApplicationCommandContext(slashCommand, _client);
                await _commandService.ExecuteAsync(context, _provider);
            });
        }
        return ValueTask.CompletedTask;
    }
}
