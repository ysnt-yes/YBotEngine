using System;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using StreamJsonRpc;

namespace YBotEngine.Services;

public class LspService(IServiceProvider serviceProvider, ILogger<LspService> logger)
{
    private readonly string _workspaceRoot = Path.Combine(Path.GetTempPath(), "ybot-lsp-sessions");

    public async Task HandleConnectionAsync(WebSocket webSocket, string sessionId, string language)
    {
        var languageProvider = serviceProvider.GetKeyedService<ILspLanguageProvider>(language.ToLower());

        if (languageProvider == null)
        {
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, $"Language '{language}' has no registered DI Key.", default);
            }
            return;
        }

        string sessionDir = Path.Combine(_workspaceRoot, sessionId);
        Directory.CreateDirectory(sessionDir);

        languageProvider.PrepareWorkspace(sessionDir);

        var startInfo = languageProvider.GetProcessStartInfo(sessionDir, Environment.ProcessId);
        using var process = new Process();
        process.StartInfo = startInfo;

        if (!process.Start()) return;

        try
        {
            var wsHandler = new WebSocketMessageHandler(webSocket);
            using var rpc = new JsonRpc(wsHandler);

            await using var nativeWebsocketStream = WebSocketStream.Create(webSocket, WebSocketMessageType.Text);
    
            using var cts = new CancellationTokenSource();

            var clientToLspTask = nativeWebsocketStream.CopyToAsync(process.StandardInput.BaseStream, cts.Token);
            var lspToClientTask = process.StandardOutput.BaseStream.CopyToAsync(nativeWebsocketStream, cts.Token);

            await Task.WhenAny(clientToLspTask, lspToClientTask);

            await cts.CancelAsync();

            try
            {
                await Task.WhenAll(clientToLspTask, lspToClientTask);
            }
            catch (OperationCanceledException)
            { }
        }
        catch (Exception ex)
        {
            logger.LogError($"JSON-RPC Execution Pipe Failure: {ex.Message}");
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(true); } catch { /* Dead */ }
            try { if (Directory.Exists(sessionDir)) Directory.Delete(sessionDir, true); } catch { /* Lock */ }
        }
    }
}
