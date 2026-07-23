using System.Collections.Concurrent;

namespace YBotEngine.Services;

public class LspService(IServiceProvider serviceProvider)
{
    private readonly ConcurrentDictionary<string, ActiveSessionState> _userSessions = new();

    private record ActiveSessionState(string Code, string PayloadType, DateTime LastAccessed);

    public ILspProvider GetProvider(string lang)
    {
        var provider = serviceProvider.GetKeyedService<ILspProvider>(lang.ToLower());
        return provider ?? 
               throw new NotSupportedException($"The language '{lang}' is not supported by this LSP system.");
    }

    public void UpdateSessionText(string sessionId, string code, string payloadType)
    {
        _userSessions[sessionId] = new ActiveSessionState(code, payloadType, DateTime.UtcNow);
    }

    public void CloseSession(string sessionId)
    {
        _userSessions.TryRemove(sessionId, out _);
    }
}