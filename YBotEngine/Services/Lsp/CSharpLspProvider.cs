using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Text;
using YBotEngine.Data;
using YBotEngine.Services.Registries;
using YScriptEngine.Abstractions;

namespace YBotEngine.Services.Lsp;

public class CSharpLspProvider(ScriptOptions sharedScriptOptions) : ILspProvider
{
    private readonly ConcurrentDictionary<string, (Solution Solution, ProjectId ProjectId)> _baseCache = new();

    public void PreCacheBaseSolution(Type payloadType)
    {
        using var workspace = new AdhocWorkspace();
        
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, scriptClassName: "Submission#0", concurrentBuild: false, metadataImportOptions: MetadataImportOptions.Public)
            .WithUsings(sharedScriptOptions.Imports);
            
        var parseOptions = new CSharpParseOptions(
            LanguageVersion.Latest, 
            kind: SourceCodeKind.Script,
            documentationMode: DocumentationMode.None );
        var projectId = ProjectId.CreateNewId();

        var projectInfo = ProjectInfo.Create(
            id: projectId, 
            version: VersionStamp.Create(), 
            name: $"Project_{payloadType.Name}", 
            assemblyName: $"Assembly_{payloadType.Name}",
            language: LanguageNames.CSharp, 
            compilationOptions: compilationOptions, 
            parseOptions: parseOptions,
            metadataReferences: sharedScriptOptions.MetadataReferences, 
            isSubmission: true, 
            hostObjectType: payloadType
        );

        _baseCache[payloadType.Name] = (workspace.CurrentSolution.AddProject(projectInfo), projectId);
    }

    private Document CreateTransientDocument(string code, string payloadType)
    {
        if (!_baseCache.TryGetValue(payloadType, out var template))
        {
            throw new KeyNotFoundException($"No baseline template pre-compiled for event payload type: '{payloadType}'.");
        }
        
        var docId = DocumentId.CreateNewId(template.ProjectId);
        var ephemeralSolution = template.Solution.AddDocument(docId, "script.csx", SourceText.From(code));
    
        return ephemeralSolution.GetDocument(docId)!;
    }

    public async Task<IEnumerable<LspCompletionItem>> GetCompletionsAsync(string code, int cursorPosition, string payloadType, CancellationToken token)
    {
        var document = CreateTransientDocument(code, payloadType);
        var completionService = CompletionService.GetService(document);
        if (completionService == null) return [];

        var completions = await completionService.GetCompletionsAsync(document, cursorPosition, cancellationToken: token);
        if (completions.ItemsList.Count == 0) return [];

        return completions.ItemsList.Select(item => new LspCompletionItem(
            Label: item.DisplayText,
            Type: item.Tags.FirstOrDefault()?.ToLower() ?? "variable",
            Detail: item.InlineDescription ?? ""
        ));
    }

    public async Task<IEnumerable<LspDiagnostic>> GetDiagnosticsAsync(string code, string payloadType, CancellationToken token)
    {
        var document = CreateTransientDocument(code, payloadType);
        
        var compilation = await document.Project.GetCompilationAsync(token);
        if (compilation == null) return [];

        var allDiagnostics = compilation.GetDiagnostics(token);

        return allDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning)
            .Select(d => new LspDiagnostic(
                From: d.Location.SourceSpan.Start,
                To: d.Location.SourceSpan.End,
                Message: d.GetMessage(),
                Severity: d.Severity.ToString().ToLower()
            ));
    }
}