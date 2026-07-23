using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using YBotEngine.Data;
using YBotEngine.Runners.Abstractions;

namespace YBotEngine.Services;

public class UserEventManager(
    IServiceProvider provider, 
    DiscordEventRegistry registry, 
    GatewayClient client,
    ILogger<UserEventManager> logger)
{
    private readonly ConcurrentDictionary<string, (EventInfo Info, Delegate Del)> _registeredScripts = new();

    public async Task RegisterScriptAsync(string eventName, string scriptName, string script, CompilerType compilerType)
    {
        if (_registeredScripts.ContainsKey(scriptName))
        {
            throw new ArgumentException($"Script '{scriptName}' is already registered.");
        }

        if (!registry.AvailableEvents.TryGetValue(eventName, out var eventData))
        {
            throw new ArgumentException($"Event '{eventName}' does not exist in NetCord.");
        }
        var clientEvent = eventData.eventInfo;
        var eventDataType = eventData.payloadType;
        
        var compiler = provider.GetRequiredKeyedService<ICompiler>(compilerType);
        var contextType = typeof(DiscordRoslynScriptContext<>).MakeGenericType(eventData.payloadType);
        var runner = await compiler.CompileAsync(script, contextType);
        
        Delegate targetDelegate;

        if (eventDataType != typeof(void))
        {
            var factoryMethod = typeof(UserEventManager)
                .GetMethod(nameof(CreatePayloadBridge), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(eventDataType);

            targetDelegate = (Delegate)factoryMethod.Invoke(this, [scriptName, eventName, runner])!;
        }
        else
        {
            targetDelegate = CreateParameterlessBridge(scriptName, eventName, runner);
        }

        _registeredScripts[scriptName] = (clientEvent, targetDelegate);
        clientEvent.AddEventHandler(client, targetDelegate);

        logger.LogInformation("Script '{ScriptName}' compiled and registered directly to event '{EventName}'.", scriptName, eventName);
    }

    public Task UnregisterScriptAsync(string scriptName)
    {
        if (!_registeredScripts.TryRemove(scriptName, out var script))
        {
            throw new ArgumentException($"Script '{scriptName}' is not registered.");
        }

        script.Info.RemoveEventHandler(client, script.Del);
        
        logger.LogInformation("Script '{ScriptName}' successfully detached and unregistered.", scriptName);
        return Task.CompletedTask;
    }

    private Delegate CreatePayloadBridge<TPayload>(string scriptName, string eventName, IRunner runner)
    {
        Func<TPayload, ValueTask> bridge = async (eventPayload) =>
        {
            if (eventPayload is Message { Author.IsBot: true }) return;
            var startTime = Environment.TickCount64;
            try
            {
                using var scope = provider.CreateScope();
                var context = new DiscordRoslynScriptContext<TPayload>(eventPayload, scope.ServiceProvider);
                await runner.ExecuteAsync(context);
                
                var elapsedMs = Environment.TickCount64 - startTime;
                
                logger.LogInformation("Script '{ScriptName}' successfully executed on '{EventName}' in {ElapsedMs}ms.", 
                    scriptName, eventName, elapsedMs);
            }
            catch (Exception ex)
            {
                var elapsedMs = Environment.TickCount64 - startTime;
                
                logger.LogError(ex, "Script '{ScriptName}' crashed on event '{EventName}' after {ElapsedMs}ms!", 
                    scriptName, eventName, elapsedMs);
            }
        };

        return bridge;
    }

    private Delegate CreateParameterlessBridge(string scriptName, string eventName, IRunner runner)
    {
        Func<ValueTask> bridge = async () =>
        {
            var startTime = Environment.TickCount64;
            try
            {
                using var scope = provider.CreateScope();
                var context = new DiscordRoslynScriptContext<object?>(null, scope.ServiceProvider);
                await runner.ExecuteAsync(context);
                
                var elapsedMs = Environment.TickCount64 - startTime;
                logger.LogInformation("Script '{ScriptName}' successfully executed on parameterless event '{EventName}' in {ElapsedMs}ms.", 
                    scriptName, eventName, elapsedMs);
            }
            catch (Exception ex)
            {
                var elapsedMs = Environment.TickCount64 - startTime;
                logger.LogError(ex, "Script '{ScriptName}' crashed on parameterless event '{EventName}' after {ElapsedMs}ms!", 
                    scriptName, eventName, elapsedMs);
            }
        };

        return bridge;
    }
}
