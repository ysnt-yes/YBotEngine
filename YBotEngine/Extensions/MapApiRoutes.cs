using Microsoft.AspNetCore.Mvc;
using YBotEngine.Data;
using YBotEngine.Services;
using YBotEngine.Services.Lsp;
using YBotEngine.Services.Registries;

namespace YBotEngine.Extensions;

public static class MapApiRoutes
{
    public static void AddApiRoutes(this WebApplication application)
    {
        var apiGroup = application.MapGroup("/api");
        
        apiGroup.MapGet("/events", (DiscordEventRegistry eventRegistry, [FromQuery] string? eventName) =>
        {
            if (!string.IsNullOrEmpty(eventName))
            {
                if (!eventRegistry.AvailableEvents.TryGetValue(eventName, out var eventInfo))
                {
                    return Results.NotFound(new { Message = $"Event '{eventName}' not found." });
                }

                return Results.Ok(new
                {
                    Name = eventName,
                    PayloadType = eventInfo.payloadType == typeof(void) ? "void" : eventInfo.payloadType.Name,
                });
            }

            var allEvents = eventRegistry.AvailableEvents.Select(kvp => new
            {
                Name = kvp.Key,
                PayloadType = kvp.Value.payloadType == typeof(void) ? "void" : kvp.Value.payloadType.Name
            });

            return Results.Ok(allEvents);
        });

        apiGroup.MapPost("/lsp", async (
            [FromQuery] string lang, 
            [FromQuery] string session, 
            [FromBody] LspQueryPayload payload,
            LspService lspService,
            HttpContext context) =>
        {
            try
            {
                var provider = lspService.GetProvider(lang);

                var token = lspService.UpdateSessionTextAndGetToken(session, payload.Code, payload.PayloadType, context.RequestAborted);

                var completionsTask = provider.GetCompletionsAsync(payload.Code, payload.CursorPosition, payload.PayloadType, token);
                var diagnosticsTask = provider.GetDiagnosticsAsync(payload.Code, payload.PayloadType, token);

                await Task.WhenAll(completionsTask, diagnosticsTask);

                return Results.Ok(new
                {
                    completions = completionsTask.Result,
                    errors = diagnosticsTask.Result
                });
            }
            catch (OperationCanceledException)
            {
                return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
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

        apiGroup.MapPost("/events/scripts/save", async (
            [FromBody] SaveScriptRequest request,
            EventScriptManager scriptManager) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Code))
                {
                    return Results.BadRequest(new { error = "Script code body cannot be empty." });
                }

                await scriptManager.RegisterOrUpdateScriptAsync(
                    eventName: request.EventName,
                    scriptName: request.ScriptId,
                    script: request.Code,
                    compilerType: "csharp"
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
    }
}