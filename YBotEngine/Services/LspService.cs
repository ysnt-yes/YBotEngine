using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace YBotEngine.Services;

public class LspService(IServiceProvider serviceProvider)
{
    private readonly ConcurrentDictionary<string, ActiveSessionState> _userSessions = new();

    private record ActiveSessionState(
        string Code, 
        string PayloadType, 
        DateTime LastAccessed, 
        CancellationTokenSource Cts);

    public ILspProvider GetProvider(string lang)
    {
        var provider = serviceProvider.GetKeyedService<ILspProvider>(lang.ToLower());
        return provider ?? 
               throw new NotSupportedException($"The language '{lang}' is not supported by this LSP system.");
    }
    
    public CancellationToken UpdateSessionTextAndGetToken(string sessionId, string code, string payloadType, CancellationToken upstreamToken)
    {
        var freshCts = new CancellationTokenSource();

        _userSessions.AddOrUpdate(
            sessionId,
            _ => new ActiveSessionState(code, payloadType, DateTime.UtcNow, freshCts),
            (_, old) => {
                try { old.Cts.Cancel(); } catch (ObjectDisposedException) { }
                try { old.Cts.Dispose(); } catch (ObjectDisposedException) { }
                return new ActiveSessionState(code, payloadType, DateTime.UtcNow, freshCts);
            }
        );

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(freshCts.Token, upstreamToken);
        return linkedCts.Token;
    }

    public void CloseSession(string sessionId)
    {
        if (!_userSessions.TryRemove(sessionId, out var sessionState)) return;
        try
        {
            sessionState.Cts.Cancel();
        }
        catch (ObjectDisposedException) { }
        finally
        {
            sessionState.Cts.Dispose();
        }
    }
}
