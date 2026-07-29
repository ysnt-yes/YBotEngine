using NetCord;
using NetCord.Rest;

namespace YBotEngine.Services.Registries;

public class SlashCommandRegistry
{


    public ApplicationCommandProperties[] GetCommands()
    {
        var properties = new SlashCommandProperties("test", "this command does not exist");
        var subcommand = new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, "hello", "This is a subcommand");
        var boolOption =
            new ApplicationCommandOptionProperties(ApplicationCommandOptionType.Boolean, "flag", "should it?");
        properties.AddOptions(subcommand.AddOptions(boolOption));
        return [properties];
    }
}