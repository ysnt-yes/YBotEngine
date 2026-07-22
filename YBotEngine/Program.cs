using Microsoft.AspNetCore.StaticFiles;
using Microsoft.CodeAnalysis.Scripting;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using YBotEngine.Data;
using YBotEngine.Extensions;
using YBotEngine.Runners.Abstractions;
using YBotEngine.Runners.Roslyn;
using YBotEngine.Services;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;
builder.Services.AddDiscordGateway(opt =>
{
    opt.Token = config["Discord:Token"];
});
builder.Services.AddApplicationCommands();

builder.Services.AddDefaultScriptOptions();

builder.Services.AddKeyedSingleton<ICompiler, RoslynCompiler>(CompilerType.Roslyn);

builder.Services.AddSingleton<DiscordEventRegistry>();
builder.Services.AddSingleton<UserScriptManager>();

builder.Services.AddSingleton<LspService>();
builder.Services.AddKeyedSingleton<ILspLanguageProvider, CSharpLspProvider>("csharp");


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseStaticFiles();
}

app.UseRouting();
//app.UseAuthorization();
app.UseWebSockets();

app.MapFallbackToFile("index.html");


app.MapGet("/api/events", (DiscordEventRegistry eventRegistry) =>
{
    return eventRegistry.AvailableEvents.Select(kvp => new
    {
        Name = kvp.Key,
        PayloadType = kvp.Value.payloadType.Name
    });
});

app.Map("/lsp", async (HttpContext context, LspService lspService) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        return Results.BadRequest("Connection protocol error: WebSocket handshake expected.");
    }

    var lang = context.Request.Query["lang"].ToString();
    var session = context.Request.Query["session"].ToString();

    if (string.IsNullOrEmpty(session) || string.IsNullOrEmpty(lang))
    {
        return Results.BadRequest("Connection rejected: Missing required 'lang' or 'session' arguments.");
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    
    await lspService.HandleConnectionAsync(webSocket, session, lang);
    
    return Results.Empty;
});
    

app.Run();