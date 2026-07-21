using System.Reflection;
using NetCord.Gateway;

namespace YBotEngine.Services;

public class DiscordEventRegistry
{
    public Dictionary<string, Type> AvailableEvents { get; } = new();

    public DiscordEventRegistry()
    {
        var clientType = typeof(GatewayClient);
        var events = clientType.GetEvents(BindingFlags.Public | BindingFlags.Instance);

        foreach (var eventInfo in events)
        {
            var invokeMethod = eventInfo.EventHandlerType?.GetMethod("Invoke");
            if (invokeMethod is null) continue;
            var parameters = invokeMethod.GetParameters();

            if (parameters.Length > 0)
            {
                AvailableEvents[eventInfo.Name] = parameters[0].ParameterType;
            }
            else
            {
                AvailableEvents[eventInfo.Name] = typeof(void);
            }
        }
    }
    
    public string? GetEventPayloadTypeName(string eventName)
    {
        return AvailableEvents.TryGetValue(eventName, out var type) ? type.FullName : null;
    }
}
