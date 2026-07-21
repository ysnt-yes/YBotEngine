using System.Reflection;
using NetCord.Gateway;

namespace YBotEngine.Services;

public class DiscordEventRegistry
{
    public Dictionary<string, Type> AvailableEvents { get; }  = new();

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
                AvailableEvents[eventInfo.Name] = typeof(object);
            }
        }
    }
    
    public string? GetEventPayloadTypeName(string eventName)
    {
        return AvailableEvents.TryGetValue(eventName, out var type) ? type.FullName : null;
    }
    
    public record CompletionMetadata(string Name, bool IsMethod, string InsertText, string TypeDetails);

    public List<CompletionMetadata> GetEventCompletionProperties(string eventName)
    {
        var list = new List<CompletionMetadata>();
    
        if (!AvailableEvents.TryGetValue(eventName, out var type))
            return list;

        var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        list.AddRange(properties.Select(prop => new CompletionMetadata(prop.Name, false, prop.Name, prop.PropertyType.Name)));

        var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => m.Name.EndsWith("Async") && !m.IsSpecialName);

        list.AddRange(methods.Select(method => new CompletionMetadata(method.Name, true, $"{method.Name}(${{1}})", "ValueTask")));

        return list;
    }
}