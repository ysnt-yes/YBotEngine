using System.Runtime.InteropServices;

namespace YBotEngine.Utils;

public static class DotnetLspLocator
{
    public static string GetLanguageServerAssemblyPath()
    {
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        
        if (string.IsNullOrEmpty(dotnetRoot))
        {
            dotnetRoot = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? @"C:\Program Files\dotnet"
                : "/usr/share/dotnet";
        }

        var sdkDirectory = Path.Combine(dotnetRoot, "sdk");

        if (!Directory.Exists(sdkDirectory))
        {
            throw new DirectoryNotFoundException($".NET SDK directory not found at: {sdkDirectory}");
        }

        var languageServerDll = Directory.GetFiles(sdkDirectory, "Microsoft.CodeAnalysis.LanguageServer.dll", SearchOption.AllDirectories)
            .OrderDescending()
            .FirstOrDefault();

        return string.IsNullOrEmpty(languageServerDll) ? 
                throw new FileNotFoundException("Could not locate 'Microsoft.CodeAnalysis.LanguageServer.dll' inside the .NET SDK directories.") : languageServerDll;
    }
}