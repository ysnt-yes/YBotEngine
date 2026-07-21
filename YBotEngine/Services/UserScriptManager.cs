using System.Reflection;
using NetCord.Gateway;
using YBotEngine.Data;
using YBotEngine.Runners.Abstractions;

namespace YBotEngine.Services;

public class UserScriptManager(IServiceProvider provider, DiscordEventRegistry registry, GatewayClient client)
{
    private readonly Dictionary<string, (EventInfo Info, Delegate Del)> _registeredScripts = new();
    public async Task RegisterScriptAsync(string eventName, string scriptName, string script, CompilerType compilerType)
    {
        if (!registry.AvailableEvents.TryGetValue(eventName, out var eventDataType))
        {
            throw new ArgumentException($"Event '{eventName}' does not exist in NetCord.");
        }
        
        var compiler = provider.GetRequiredKeyedService<ICompiler>(compilerType);
        
        var runner = await compiler.CompileAsync(script, eventDataType);
        
        var clientEvent = client.GetType().GetEvent(eventName)!;
        Delegate targetDelegate;
        
        var invokeMethod = clientEvent.EventHandlerType!.GetMethod("Invoke")!;
        if (invokeMethod.GetParameters().Length > 0)
        {
            // Payload events
            Func<object, ValueTask> eventBridge = async (eventPayload) =>
            {
                using var scope = provider.CreateScope();
                var context = new DiscordEventContext(eventPayload, scope.ServiceProvider);
                await runner.ExecuteAsync(context);
            };
            targetDelegate = Delegate.CreateDelegate(clientEvent.EventHandlerType!, eventBridge.Target, eventBridge.Method);
        }
        else
        {
            // Payload-less events
            Func<ValueTask> parameterlessBridge = async () =>
            {
                using var scope = provider.CreateScope();
                var context = new DiscordEventContext(new object(), scope.ServiceProvider);
                await runner.ExecuteAsync(context);
            };
            targetDelegate = Delegate.CreateDelegate(clientEvent.EventHandlerType!, parameterlessBridge.Target, parameterlessBridge.Method);
        }
        _registeredScripts[scriptName] = (clientEvent, targetDelegate);
        clientEvent.AddEventHandler(client, targetDelegate);
    }
    
    public bool IsRegistered(string scriptName) => _registeredScripts.ContainsKey(scriptName);

    public Task UnregisterScriptAsync(string scriptName)
    {
        if (!_registeredScripts.TryGetValue(scriptName, out var script))
        {
            throw new ArgumentException($"Script '{scriptName}' is not registered.");
        }
        
        script.Info.RemoveEventHandler(client, script.Del);
        
        _registeredScripts.Remove(scriptName);
        return Task.CompletedTask;
    }
}