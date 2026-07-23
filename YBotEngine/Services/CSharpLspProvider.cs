using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Text;
using YBotEngine.Data;

namespace YBotEngine.Services;

public class CSharpLspProvider : ILspProvider
{
    private readonly ConcurrentDictionary<string, (Solution Solution, ProjectId ProjectId)> _baseCache = new();
    private readonly ScriptOptions _sharedScriptOptions;

    public CSharpLspProvider(ScriptOptions sharedScriptOptions, DiscordEventRegistry registry)
    {
        _sharedScriptOptions = sharedScriptOptions;

        foreach (var payloadType in registry.AvailableEvents.Values.Select(r => r.payloadType).Distinct())
        {
            PreCacheBaseSolution(payloadType);
        }
    }

    private void PreCacheBaseSolution(Type payloadType)
    {
        var isVoid = payloadType == typeof(void) || payloadType.FullName == "System.Void";
        var typeKey = isVoid ? "void" : payloadType.Name;
        
        using var workspace = new AdhocWorkspace();
        var compileTimeType = isVoid ? typeof(EmptyPayload) : payloadType;
        var globalHostType = typeof(DiscordRoslynScriptContext<>).MakeGenericType(compileTimeType);
        
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, scriptClassName: "Submission#0", concurrentBuild: false, metadataImportOptions: MetadataImportOptions.Public)
            .WithUsings(_sharedScriptOptions.Imports);
            
        var parseOptions = new CSharpParseOptions(
            LanguageVersion.Latest, 
            kind: SourceCodeKind.Script,
            documentationMode: DocumentationMode.None );
        var projectId = ProjectId.CreateNewId();

        var projectInfo = ProjectInfo.Create(
            id: projectId, 
            version: VersionStamp.Create(), 
            name: $"Project_{typeKey}", 
            assemblyName: $"Assembly_{typeKey}",
            language: LanguageNames.CSharp, 
            compilationOptions: compilationOptions, 
            parseOptions: parseOptions,
            metadataReferences: _sharedScriptOptions.MetadataReferences, 
            isSubmission: true, 
            hostObjectType: globalHostType
        );

        _baseCache[typeKey] = (workspace.CurrentSolution.AddProject(projectInfo), projectId);
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

    public async Task<IEnumerable<LspCompletionItem>> GetCompletionsAsync(string code, int cursorPosition, string payloadType)
    {
        var document = CreateTransientDocument(code, payloadType);
        var completionService = CompletionService.GetService(document);
        if (completionService == null) return [];

        var completions = await completionService.GetCompletionsAsync(document, cursorPosition);
        if (completions.ItemsList.Count == 0) return [];

        return completions.ItemsList.Select(item => new LspCompletionItem(
            Label: item.DisplayText,
            Type: item.Tags.FirstOrDefault()?.ToLower() ?? "variable",
            Detail: item.InlineDescription ?? ""
        ));
    }

    public async Task<IEnumerable<LspDiagnostic>> GetDiagnosticsAsync(string code, string payloadType)
    {
        var document = CreateTransientDocument(code, payloadType);
        
        var syntaxTree = await document.GetSyntaxTreeAsync();
        var syntaxDiags = syntaxTree?.GetDiagnostics() ?? [];

        var semanticModel = await document.GetSemanticModelAsync();
        var semanticDiags = semanticModel?.GetDiagnostics() ?? Enumerable.Empty<Diagnostic>();

        return syntaxDiags.Concat(semanticDiags).Select(d => new LspDiagnostic(
            From: d.Location.SourceSpan.Start,
            To: d.Location.SourceSpan.End,
            Message: d.GetMessage(),
            Severity: d.Severity.ToString().ToLower()
        ));
    }
}