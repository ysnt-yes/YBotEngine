using NetCord.Gateway;
using NetCord.Hosting.Gateway;
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

builder.Services.AddSingleton<DiscordEventRegistry>();
builder.Services.AddSingleton<SlashCommandRegistry>();

builder.Services.AddSingleton<EventScriptManager>();
builder.Services.AddSingleton<SlashCommandScriptManager<SlashCommandContext>>();

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

var commandService = app.Services.GetRequiredService<ApplicationCommandService<SlashCommandContext>>();
commandService.AddModules(typeof(Program).Assembly);

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


app.Run();