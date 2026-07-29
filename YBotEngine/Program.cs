using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using YBotEngine.Data;
using YBotEngine.Extensions;
using YBotEngine.Factories;
using YScriptEngine.Roslyn;
using YBotEngine.Services;
using YBotEngine.Services.Events;
using YBotEngine.Services.Lsp;
using YBotEngine.Services.Registries;
using YScriptEngine.Abstractions;
using YScriptEngine.NodeGraph;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;
builder.Services.AddDiscordGateway(opt =>
{
    opt.Token = config["Discord:Token"];
    opt.Intents = GatewayIntents.All;
});
builder.Services.AddSingleton<ApplicationCommandService<SlashCommandContext>>();

builder.Services.AddDefaultScriptOptions();

builder.Services.AddKeyedSingleton<ICompiler, RoslynCompiler>("csharp");
builder.Services.AddSingleton<IScriptContextFactory, ScriptContextFactory>();

builder.Services.AddSingleton<INodeRegistry, NodeRegistry>();
builder.Services.AddKeyedSingleton<ICompiler, NodeToRoslynCompiler>("node_graph", (sp, key) =>
{
    var baseCsharpCompiler = sp.GetRequiredKeyedService<ICompiler>("csharp");
    var activeRegistry = sp.GetRequiredService<INodeRegistry>();
    
    return new NodeToRoslynCompiler(baseCsharpCompiler, activeRegistry);
});

builder.Services.AddSingleton<DiscordEventRegistry>();
builder.Services.AddSingleton<SlashCommandRegistry>();

builder.Services.AddSingleton<InteractionInterceptor>();

builder.Services.AddSingleton<CommandRegistrationService>();

builder.Services.AddSingleton<EventScriptManager>();
builder.Services.AddSingleton<SlashCommandScriptManager>();

builder.Services.AddSingleton<IEventBus, EventBus>();
builder.Services.AddSingleton<GatewayEventBus>();


builder.Services.AddSingleton<LspService>();
builder.Services.AddKeyedSingleton<ILspProvider, CSharpLspProvider>("csharp");


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseStaticFiles();
}

app.UseRouting();
//app.UseAuthorization();
app.UseWebSockets();

app.MapFallbackToFile("index.html");

app.AddApiRoutes();

app.AddSlashCommandToService("fromservice", "This one is from the service",
    async (SlashCommandContext context) =>
    {
        await context.Interaction.SendResponseAsync(InteractionCallback.Message("This is from the service"));
    });

const string testScript = "await Data.Interaction.SendResponseAsync(InteractionCallback.Message(\"This is from the script manager\"));";

var slashCommandManager = app.Services.GetRequiredService<SlashCommandScriptManager>();
await slashCommandManager.CompileAndRegisterScriptAsync("test", testScript, "csharp");

var registry = app.Services.GetRequiredService<DiscordEventRegistry>();
var roslynLsp = (CSharpLspProvider)app.Services.GetRequiredKeyedService<ILspProvider>("csharp");


foreach (var payloadType in registry.AvailableEvents.Values.Select(r => r.payloadType).Distinct())
{
    var isVoid = payloadType == typeof(void) || payloadType.FullName == "System.Void";
    var compileTimeType = isVoid ? typeof(EmptyPayload) : payloadType;
    var globalHostType = typeof(RoslynScriptContext<>).MakeGenericType(compileTimeType);
    roslynLsp.PreCacheBaseSolution(globalHostType);
}


_ = app.Services.GetRequiredService<GatewayEventBus>();
_ = app.Services.GetRequiredService<IEventBus>();
_ = app.Services.GetRequiredService<InteractionInterceptor>();

var registration = app.Services.GetRequiredService<CommandRegistrationService>();
await registration.SyncCommandsAsync();


app.Run();