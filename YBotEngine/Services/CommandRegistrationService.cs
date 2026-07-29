using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using YBotEngine.Extensions;
using YBotEngine.Services.Registries;

namespace YBotEngine.Services;

public class CommandRegistrationService(
    ApplicationCommandService<SlashCommandContext> applicationCommandService, 
    RestClient client,
    SlashCommandRegistry slashCommandRegistry,
    ILogger<CommandRegistrationService> logger
    )
{
    public async Task SyncCommandsAsync()
    {
        var properties = await applicationCommandService.GetAllCommandsAsync();

        var custom = slashCommandRegistry.GetCommands();
        
        var concat = properties.Concat(custom);

        foreach (var property in concat)
        {
            logger.LogDebug(property.Name);
        }
        
        await client.BulkOverwriteGlobalApplicationCommandsAsync(((IEntityToken)client.Token!).Id ,concat);
    }
}