using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace YBotEngine.Services;

public class CSharpLspProvider : ILspLanguageProvider
{
    public void PrepareWorkspace(string sessionDir)
    {
        var projectXml = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
            new XElement("PropertyGroup",
                new XElement("TargetFramework", "net10.0"),
                new XElement("ImplicitUsings", "enable"),
                new XElement("Nullable", "enable")
            )
        );

        var itemGroup = new XElement("ItemGroup",
            new XElement("Reference", new XAttribute("Include", "YBotEngine"),
                new XElement("HintPath", "/src/bin/Debug/net10.0/YBotEngine.dll")
            )
        );

        projectXml.Add(itemGroup);

        var targetFilePath = Path.Combine(sessionDir, "Project.csproj");
        projectXml.Save(targetFilePath);
    }

    public ProcessStartInfo GetProcessStartInfo(string sessionDir, int hostProcessId)
    {
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT") 
                         ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\Program Files\dotnet" : "/usr/share/dotnet");

        var sdkDirectory = Path.Combine(dotnetRoot, "sdk");
        
        var lspDllPath = Directory.GetFiles(sdkDirectory, "Microsoft.CodeAnalysis.LanguageServer.dll", SearchOption.AllDirectories)
                                     .OrderDescending()
                                     .FirstOrDefault();

        if (string.IsNullOrEmpty(lspDllPath))
            throw new FileNotFoundException("Could not resolve 'Microsoft.CodeAnalysis.LanguageServer.dll' inside the .NET 10 SDK directory structure.");

        return new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{lspDllPath}\" --hostPID {hostProcessId}",
            WorkingDirectory = sessionDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }
}
