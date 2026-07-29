using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using NetCord.Gateway;
using YBotEngine.Data;
using YBotEngine.Factories;
using YBotEngine.Services.Events;
using YBotEngine.Services.Registries;
using YScriptEngine.Abstractions;

namespace YBotEngine.Services;

public class EventScriptManager
{
    private readonly IServiceProvider _provider;
    private readonly DiscordEventRegistry _registry;
    private readonly GatewayClient _client;
    private readonly IScriptContextFactory _contextFactory;
    private readonly ILogger<EventScriptManager> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Func<object?, ValueTask>>> _eventGroupedScripts = new(StringComparer.OrdinalIgnoreCase);

    public EventScriptManager(
        IServiceProvider provider,
        DiscordEventRegistry registry,
        GatewayClient client,
        IScriptContextFactory contextFactory,
        IEventBus eventBus,
        ILogger<EventScriptManager> logger)
    {
        _provider = provider;
        _registry = registry;
        _client = client;
        _contextFactory = contextFactory;
        _logger = logger;
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        eventBus.Subscribe<GatewayBusEvent>(RouteBusEventAsync);
    }

    public async Task RegisterScriptAsync(string eventName, string scriptName, string script, string compilerType)
    {
        if (!_registry.AvailableEvents.TryGetValue(eventName, out var eventData))
        {
            throw new ArgumentException($"Event '{eventName}' does not exist in NetCord.");
        }

        var eventBucket = _eventGroupedScripts.GetOrAdd(eventName, _ => new(StringComparer.OrdinalIgnoreCase));

        if (eventBucket.ContainsKey(scriptName))
        {
            throw new ArgumentException($"Script '{scriptName}' is already registered under event '{eventName}'.");
        }

        var eventDataType = eventData.payloadType;
        var compiler = _provider.GetRequiredKeyedService<ICompiler>(compilerType);
        var contextType = typeof(RoslynScriptContext<>).MakeGenericType(eventDataType);
        var runner = await compiler.CompileAsync(script, contextType);

        var factoryMethod = typeof(EventScriptManager)
            .GetMethod(nameof(CreatePayloadBridge), BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Could not locate method {nameof(CreatePayloadBridge)}.");

        var genericFactoryMethod = factoryMethod.MakeGenericMethod(eventDataType);
        var targetDelegate = (Func<object?, ValueTask>)genericFactoryMethod.Invoke(this, [scriptName, eventName, runner])!;

        if (!eventBucket.TryAdd(scriptName, targetDelegate))
        {
            throw new ArgumentException($"Script '{scriptName}' is already registered under event '{eventName}'.");
        }

        _logger.LogInformation("Script '{ScriptName}' compiled and registered directly to event '{EventName}'.", scriptName, eventName);
    }

    public void UnregisterScript(string eventName, string scriptName)
    {
        if (!_eventGroupedScripts.TryGetValue(eventName, out var bucket)) return;
        if (!bucket.TryRemove(scriptName, out _)) return;

        _logger.LogInformation("Script '{ScriptName}' unbound from event '{EventName}'.", scriptName, eventName);
    }
    
    public async Task RegisterOrUpdateScriptAsync(string eventName, string scriptName, string script, string compilerType)
    {
        if (_eventGroupedScripts.TryGetValue(eventName, out var bucket) && bucket.ContainsKey(scriptName))
        {
            UnregisterScript(eventName, scriptName);
        }

        await RegisterScriptAsync(eventName, scriptName, script, compilerType);
    }

    private async Task RouteBusEventAsync(GatewayBusEvent evt, CancellationToken token)
    {
        if (!_eventGroupedScripts.TryGetValue(evt.EventName, out var scriptBucket) || scriptBucket.IsEmpty)
        {
            return;
        }

        foreach (var (scriptName, scriptBridge) in scriptBucket)
        {
            try
            {
                await scriptBridge(evt.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled pipeline exception inside script runner layout for '{ScriptName}'", scriptName);
            }
        }
    }

    private Func<object?, ValueTask> CreatePayloadBridge<TPayload>(string scriptName, string eventName, IScript runner) where TPayload : notnull
    {
        if (typeof(TPayload) == typeof(void))
        {
            return async (_) =>
            {
                var startTime = Stopwatch.GetTimestamp();
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var context = _contextFactory.CreateContext(typeof(TPayload), null!, scope.ServiceProvider);
                    await runner.ExecuteAsync(context);
                    
                    var elapsed = Stopwatch.GetElapsedTime(startTime);
                    _logger.LogInformation("Script '{ScriptName}' successfully executed on parameterless event '{EventName}' in {ElapsedMs}ms.", 
                        scriptName, eventName, elapsed.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    var elapsed = Stopwatch.GetElapsedTime(startTime);
                    _logger.LogError(ex, "Script '{ScriptName}' crashed on parameterless event '{EventName}' after {ElapsedMs}ms!", 
                        scriptName, eventName, elapsed.TotalMilliseconds);
                }
            };
        }

        return async (rawPayload) =>
        {
            if (rawPayload is not TPayload eventPayload) return;
            if (eventPayload is Message { Author.IsBot: true }) return;
            
            var startTime = Stopwatch.GetTimestamp();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = _contextFactory.CreateContext(typeof(TPayload), eventPayload, scope.ServiceProvider);
                await runner.ExecuteAsync(context);
                
                var elapsed = Stopwatch.GetElapsedTime(startTime);
                _logger.LogInformation("Script '{ScriptName}' successfully executed on '{EventName}' in {ElapsedMs}ms.", 
                    scriptName, eventName, elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                var elapsed = Stopwatch.GetElapsedTime(startTime);
                _logger.LogError(ex, "Script '{ScriptName}' crashed on event '{EventName}' after {ElapsedMs}ms!", 
                    scriptName, eventName, elapsed.TotalMilliseconds);
            }
        };
    }
}
