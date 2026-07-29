using System.Collections.Concurrent;
using System.Reflection;
using NetCord.Gateway;
using YBotEngine.Data;
using YBotEngine.Services.Registries;

namespace YBotEngine.Services.Events;

public class GatewayEventBus
{

    public GatewayEventBus(
        IEventBus bus, 
        DiscordEventRegistry registry, 
        GatewayClient client, 
        ILogger<GatewayEventBus> logger)
    {
        foreach (var keyValuePair in registry.AvailableEvents)
        {
            var (eventInfo, payloadType) = keyValuePair.Value;

            logger.LogDebug("Hooking into Netcord event: {EventInfoName}", eventInfo.Name);
            Delegate handlerDelegate;
            
            if (payloadType == typeof(void))
            {
                handlerDelegate = () =>
                {
                    bus.Publish(new GatewayBusEvent(keyValuePair.Key, null));
                    return ValueTask.CompletedTask;
                };
            }
            else
            {
                var eventHandlerType = eventInfo.EventHandlerType!;
                var invokeMethod = eventHandlerType.GetMethod("Invoke")!;
                var expectedType = invokeMethod.GetParameters()[0].ParameterType;

                var method = typeof(GatewayEventBus)
                    .GetMethod(nameof(CreateGenericHandler), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(expectedType, payloadType);

                handlerDelegate = (Delegate)method.Invoke(null, [keyValuePair.Key, bus])!;
            }

            
            eventInfo.AddEventHandler(client, handlerDelegate);
        }
    }
    
    private static Func<TNetcord, ValueTask> CreateGenericHandler<TNetcord, TPayload>(string eventName, IEventBus eventBus) 
        where TNetcord : notnull
    {
        return (payload) =>
        {
            if (typeof(TPayload) == typeof(TNetcord))
            {
                eventBus.Publish(new GatewayBusEvent(eventName, payload));
            }
            else if (payload is TPayload matchingPayload)
            {
                eventBus.Publish(new GatewayBusEvent(eventName, matchingPayload));
            }

            return ValueTask.CompletedTask;
        };
    }
}