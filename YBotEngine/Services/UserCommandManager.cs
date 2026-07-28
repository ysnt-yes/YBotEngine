using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using YBotEngine.Data;
using YBotEngine.Factories;
using YBotEngine.Runners.Abstractions;
using YBotEngine.Services;

namespace YBotEngine.Managers;

public class UserCommandManager(
    IServiceProvider serviceProvider,
    IScriptContextFactory contextFactory,
    IServiceScopeFactory scopeFactory)
{
    
    private readonly ConcurrentDictionary<string, IRunner> _activeCommandRunners = new();
    public async Task CompileAndRegisterScriptAsync(string pathKey, string rawSource, string compilerType)
    {
        if (string.IsNullOrWhiteSpace(pathKey)) 
            throw new ArgumentException("Path key cannot be empty.");

        var compiler = serviceProvider.GetKeyedService<ICompiler>(compilerType.ToLower().Trim())
            ?? throw new NotSupportedException($"The script compiler backend engine type '{compilerType}' is not registered.");

        Type contextType = typeof(DiscordRoslynScriptContext<ApplicationCommandContext>);
        IRunner compiledRunner = await compiler.CompileAsync(rawSource, contextType);

        _activeCommandRunners[pathKey.ToLower().Trim()] = compiledRunner;
    }

    public void UnregisterCommandPath(string pathKey)
    {
        _activeCommandRunners.TryRemove(pathKey.ToLower().Trim(), out _);
    }

    public void ClearAll() => _activeCommandRunners.Clear();

    public async Task<string> CreateOrUpdateCommandAsync(
        string? existingDiscordId,
        string commandPath,
        string description,
        List<UiOptionParameterDto> options,
        RestClient restClient,
        ulong applicationId,
        ulong? guildId = null)
    {
        SlashCommandProperties discordProps = BuildSlashCommandProperties(commandPath, description, options);

        ApplicationCommand responseCommand;

        if (!string.IsNullOrEmpty(existingDiscordId) && ulong.TryParse(existingDiscordId, out ulong cmdId))
        {
            responseCommand = guildId.HasValue
                ? await restClient.ModifyGuildApplicationCommandAsync(applicationId, guildId.Value, cmdId, discordProps)
                : await restClient.ModifyGlobalApplicationCommandAsync(applicationId, cmdId, discordProps);
        }
        else
        {
            responseCommand = guildId.HasValue
                ? await restClient.CreateGuildApplicationCommandAsync(applicationId, guildId.Value, discordProps)
                : await restClient.CreateGlobalApplicationCommandAsync(applicationId, discordProps);
        }

        return responseCommand.Id.ToString();
    }

    private static SlashCommandProperties BuildSlashCommandProperties(string commandPath, string description, List<UiOptionParameterDto> options)
    {
        var segments = commandPath.Split(':');
        string rootName = segments[0].ToLower().Trim();

        if (segments.Length == 1)
        {
            var props = new SlashCommandProperties(rootName, description);
            foreach (var opt in options)
            {
                props.AddOption(new ApplicationCommandOptionProperties(opt.Type, opt.Name, opt.Description) { Required = opt.IsRequired });
            }
            return props;
        }

        var baseProps = new SlashCommandProperties(rootName, "Container group");

        if (segments.Length == 2)
        {
            var subCommand = new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, segments[1].ToLower().Trim(), description);
            foreach (var opt in options)
            {
                subCommand.AddOption(new ApplicationCommandOptionProperties(opt.Type, opt.Name, opt.Description) { Required = opt.IsRequired });
            }
            baseProps.AddOption(subCommand);
        }
        else if (segments.Length == 3)
        {
            var subGroup = new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommandGroup, segments[1].ToLower().Trim(), "Group folder");
            var subCommand = new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, segments[2].ToLower().Trim(), description);
            
            foreach (var opt in options)
            {
                subCommand.AddOption(new ApplicationCommandOptionProperties(opt.Type, opt.Name, opt.Description) { Required = opt.IsRequired });
            }
            
            subGroup.AddOption(subCommand);
            baseProps.AddOption(subGroup);
        }

        return baseProps;
    }
    public async Task RouteCommandInteractionAsync(SlashCommandContext context)
    {
        string lookupPathKey = GetFlattenedCommandKey(context);

        if (_activeCommandRunners.TryGetValue(lookupPathKey, out var runner))
        {
            using var scope = scopeFactory.CreateScope();
            var scopedProvider = scope.ServiceProvider;
            
            IRunnerContext scriptContext = contextFactory.CreateContext(
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
                try { await context.Interaction.; } catch { }
            }
        }
        else
        {
            await context.Interaction.Response.SendMessageAsync($"Routing Failure: No running script matches '/{lookupPathKey.Replace(':', ' ')}'.");
        }
    }

    private static string GetFlattenedCommandKey(SlashCommandContext context)
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
}
