using NetCord;
using NetCord.Gateway;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using YBotEngine.Data;

namespace YBotEngine.Services.Events;

public class InteractionInterceptor
{
    private readonly SlashCommandScriptManager _scriptManager;
    private readonly ApplicationCommandService<SlashCommandContext> _nativeCommandService;
    private readonly GatewayClient _gatewayClient;
    private readonly ILogger<InteractionInterceptor> _logger;

    public InteractionInterceptor(
        IEventBus eventBus,
        SlashCommandScriptManager scriptManager,
        ApplicationCommandService<SlashCommandContext> nativeCommandService,
        GatewayClient gatewayClient, ILogger<InteractionInterceptor> logger)
    {
        _scriptManager = scriptManager;
        _nativeCommandService = nativeCommandService;
        _gatewayClient = gatewayClient;
        _logger = logger;

        eventBus.Subscribe<GatewayBusEvent>(OnBusEventReceivedAsync);
    }

    private async Task OnBusEventReceivedAsync(GatewayBusEvent evt, CancellationToken token)
    {
        if (evt.Data is not Interaction interaction) return;
        
        switch (interaction)
        {
            case ApplicationCommandInteraction commandInteraction:
                await HandleApplicationCommandAsync(commandInteraction);
                break;

            case ComponentInteraction componentInteraction:
                //TODO: Implement
                //TODO: Buttons, Dropdowns, Modals
                //await HandleComponentInteractionAsync(componentInteraction);
                break;
            
            case AutocompleteInteraction autocompleteInteraction:
                //TODO: Implement
                //await HandleAutocompleteAsync(autocompleteInteraction);
                break;
        }

        
    }
    
    private async Task HandleApplicationCommandAsync(ApplicationCommandInteraction command)
    {
        switch (command)
        {
            case SlashCommandInteraction slash:
                var slashContext = new SlashCommandContext(slash, _gatewayClient);
                var slashKey = _scriptManager.GetFlattenedCommandKey(slashContext);

                if (_scriptManager.HasActiveRunner(slashKey))
                    await _scriptManager.RunScriptAsync(slashKey, slashContext);
                else
                    await _nativeCommandService.ExecuteAsync(slashContext);
                break;

            case UserCommandInteraction userMenu:
                var userContext = new UserCommandContext(userMenu, _gatewayClient);
                //TODO: Implement
                break;

            case MessageCommandInteraction messageMenu:
                var messageContext = new MessageCommandContext(messageMenu, _gatewayClient);
                //TODO: Implement
                break;
        }
    }
}