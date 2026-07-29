using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace YBotEngine.Extensions;

public static class ApplicationCommandServiceExtensions
{
    public static Task<ApplicationCommandProperties[]> GetAllCommandsAsync<TContext>(this ApplicationCommandService<TContext> applicationCommandService) where TContext : IApplicationCommandContext
    {
        return Task.WhenAll(applicationCommandService.GetCommands().Select(c => c.GetRawValueAsync().AsTask()));
    }

    public static void AddSlashCommandToService(this IHost host, string name, string description,
        Delegate handler)
    {
        var service = host.Services.GetRequiredService<ApplicationCommandService<SlashCommandContext>>();
        service.AddSlashCommand(new SlashCommandBuilder(name, description, handler));
    }
    
    public static void AddSlashCommandGroupToService(this IHost host, string name, string description,
        Delegate handler)
    {
        var service = host.Services.GetRequiredService<ApplicationCommandService<SlashCommandContext>>();
        service.AddSlashCommandGroup(new SlashCommandGroupBuilder(name, description));
    }
    
    public static void AddUserCommandToService(this IHost host, string name, Delegate handler)
    {
        var service = host.Services.GetRequiredService<ApplicationCommandService<UserCommandContext>>();
        service.AddUserCommand(new UserCommandBuilder(name, handler));
    }
    
    
}