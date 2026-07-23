using Microsoft.AspNetCore.Mvc;
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
builder.Services.AddSingleton<UserEventManager>();

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


app.MapGet("/api/events", (DiscordEventRegistry eventRegistry) =>
{
    return eventRegistry.AvailableEvents.Select(kvp => new
    {
        Name = kvp.Key,
        PayloadType = kvp.Value.payloadType == typeof(void) ? "void" : kvp.Value.payloadType.Name
    });
});

app.MapPost("/api/lsp", async (
    [FromQuery] string lang, 
    [FromQuery] string session, 
    [FromBody] LspQueryPayload payload,
    LspService lspService) =>
{
    try
    {
        var provider = lspService.GetProvider(lang);

        lspService.UpdateSessionText(session, payload.Code, payload.PayloadType);

        var completionsTask = provider.GetCompletionsAsync(payload.Code, payload.CursorPosition, payload.PayloadType);
        var diagnosticsTask = provider.GetDiagnosticsAsync(payload.Code, payload.PayloadType);

        await Task.WhenAll(completionsTask, diagnosticsTask);

        return Results.Ok(new
        {
            completions = completionsTask.Result,
            errors = diagnosticsTask.Result
        });
    }
    catch (NotSupportedException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem(title: "Internal compiler error occurred.", detail: ex.Message, statusCode: 500);
    }
});

app.MapPost("/api/scripts/save", async (
    [FromBody] SaveScriptRequest request,
    UserEventManager eventManager) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Results.BadRequest(new { error = "Script code body cannot be empty." });
        }

        await eventManager.RegisterScriptAsync(
            eventName: request.EventName,
            scriptName: request.ScriptId,
            script: request.Code,
            compilerType: CompilerType.Roslyn
        );

        return Results.Ok(new { success = true, message = "Script registered successfully." });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Failed to persist script changes.", 
            detail: ex.Message, 
            statusCode: 500
        );
    }
});

app.Run();