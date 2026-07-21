using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using YBotEngine.Data;
using YBotEngine.Runners.Abstractions;

namespace YBotEngine.Services;

public class UserScriptManager(
    IServiceProvider provider, 
    DiscordEventRegistry registry, 
    GatewayClient client,
    ILogger<UserScriptManager> logger)
{
    private readonly ConcurrentDictionary<string, (EventInfo Info, Delegate Del)> _registeredScripts = new();

    public async Task RegisterScriptAsync(string eventName, string scriptName, string script, CompilerType compilerType)
    {
        if (_registeredScripts.ContainsKey(scriptName))
        {
            throw new ArgumentException($"Script '{scriptName}' is already registered.");
        }

        if (!registry.AvailableEvents.TryGetValue(eventName, out var eventDataType))
        {
            throw new ArgumentException($"Event '{eventName}' does not exist in NetCord.");
        }

        var compiler = provider.GetRequiredKeyedService<ICompiler>(compilerType);
        var runner = await compiler.CompileAsync(script, eventDataType);

        var clientEvent = client.GetType().GetEvent(eventName) 
            ?? throw new InvalidOperationException($"Netcord event '{eventName}' metadata not found.");

        Delegate targetDelegate;

        if (eventDataType != typeof(void))
        {
            var factoryMethod = typeof(UserScriptManager)
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
            var startTime = Environment.TickCount64;
            try
            {
                using var scope = provider.CreateScope();
                var context = new DiscordEventContext<TPayload>(eventPayload, scope.ServiceProvider);
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
                var context = new DiscordEventContext<object?>(null, scope.ServiceProvider);
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
