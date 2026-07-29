using NetCord;
using NetCord.Gateway;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using YBotEngine.Data;

namespace YBotEngine.Services.Events;

public class InteractionInterceptor
{
    private readonly SlashCommandScriptManager<SlashCommandContext> _scriptManager;
    private readonly ApplicationCommandService<SlashCommandContext> _nativeCommandService;
    private readonly GatewayClient _gatewayClient;
    private readonly ILogger<InteractionInterceptor> _logger;

    public InteractionInterceptor(
        IEventBus eventBus,
        SlashCommandScriptManager<SlashCommandContext> scriptManager,
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
        if (evt.Data is not SlashCommandInteraction slashInteraction) return;

        var context = new SlashCommandContext(slashInteraction, _gatewayClient);

        var lookupPathKey = _scriptManager.GetFlattenedCommandKey(context);

        if (_scriptManager.HasActiveRunner(lookupPathKey))
        {
            await _scriptManager.RouteCommandInteractionAsync(context);
        }
        else
        {
            await _nativeCommandService.ExecuteAsync(context);
        }
    }
}