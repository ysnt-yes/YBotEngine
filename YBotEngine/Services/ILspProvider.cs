namespace YBotEngine.Services;

public interface ILspProvider
{
    Task<IEnumerable<LspCompletionItem>> GetCompletionsAsync(string code, int cursorPosition, string payloadType);
    Task<IEnumerable<LspDiagnostic>> GetDiagnosticsAsync(string code, string payloadType);
}

public record LspCompletionItem(string Label, string Type, string Detail = "");
public record LspDiagnostic(int From, int To, string Message, string Severity);


public record LspQueryPayload(string Code, int CursorPosition, string PayloadType);