using System.Diagnostics;

namespace YBotEngine.Services;

public interface ILspLanguageProvider
{
    public void PrepareWorkspace(string path);
    ProcessStartInfo GetProcessStartInfo(string sessionDir, int processId);
}