using System.Collections.Concurrent;
using System.Reflection;
using NetCord.Gateway;
using YBotEngine.Data;
using YBotEngine.Services.Registries;

namespace YBotEngine.Services.Events;

public class GatewayEventBus
{
    private readonly ConcurrentDictionary<string, Delegate> _allocatedDelegates = new(StringComparer.OrdinalIgnoreCase);
    public GatewayEventBus(
        IEventBus bus, 
        DiscordEventRegistry registry, 
        GatewayClient client, 
        ILogger<GatewayEventBus> logger)
    {
        foreach (var keyValuePair in registry.AvailableEvents)
        {
            var (eventInfo, payloadType) = keyValuePair.Value;

            logger.LogInformation($"Registering event {eventInfo.Name}");
            Delegate handlerDelegate;
            
            if (payloadType == typeof(void))
            {
                var action = () =>
                {
                    bus.Publish(new GatewayBusEvent(keyValuePair.Key, null));
                    return ValueTask.CompletedTask;
                };
                handlerDelegate = action;
            }
            else
            {
                var method = typeof(GatewayEventBus)
                    .GetMethod(nameof(CreateGenericHandler), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(payloadType);

                handlerDelegate = (Delegate)method.Invoke(null, [keyValuePair.Key, bus])!;
            }

            _allocatedDelegates[keyValuePair.Key] = handlerDelegate;
            eventInfo.AddEventHandler(client, handlerDelegate);
        }
    }
    
    private static Func<T, ValueTask> CreateGenericHandler<T>(string eventName, IEventBus eventBus) where T : notnull
    {
        return (payload) =>
        {
            eventBus.Publish(new GatewayBusEvent(eventName, payload));
            return ValueTask.CompletedTask;
        };
    }
}