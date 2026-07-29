using System.Text;
using YScriptEngine.Abstractions;

namespace YScriptEngine.NodeGraph;

public class NodeToRoslynCompiler(ICompiler baseCompiler, INodeRegistry nodeRegistry) : ICompiler
{
    private readonly ICompiler _baseCompiler = baseCompiler;
    private readonly INodeRegistry _nodeRegistry = nodeRegistry;

    public Task<IScript> CompileAsync(string scriptCode, Type contextType)
    {
        return _baseCompiler.CompileAsync(scriptCode, contextType);
    }

    public Task<IScript> CompileGraphAsync(GraphConfiguration graph, Type contextType)
    {
        if (graph == null) throw new ArgumentNullException(nameof(graph));

        string generatedCode = TranspileGraphToCSharp(graph);
        return _baseCompiler.CompileAsync(generatedCode, contextType);
    }

    private string TranspileGraphToCSharp(GraphConfiguration graph)
    {
        if (string.IsNullOrEmpty(graph.StartNodeId) || !graph.Nodes.ContainsKey(graph.StartNodeId))
        {
            return string.Empty;
        }

        var codeBuilder = new StringBuilder();
        
        var catalog = _nodeRegistry.GetCatalog();
        
        var metadataLookup = new Dictionary<string, NodeMetadata>();
        foreach (var nodeList in catalog.Values)
        {
            foreach (var meta in nodeList)
            {
                metadataLookup[meta.NodeName] = meta;
            }
        }

        string currentNodeId = graph.StartNodeId;
        var visited = new HashSet<string>(); 

        while (!string.IsNullOrEmpty(currentNodeId) && graph.Nodes.TryGetValue(currentNodeId, out var instance))
        {
            if (!visited.Add(currentNodeId)) break; 

            if (metadataLookup.TryGetValue(instance.NodeName, out var metadata))
            {
                string singleLineOfCode = metadata.CodeTemplate(instance.PinVariableAssignments);
                codeBuilder.AppendLine(singleLineOfCode);
            }
            else
            {
                codeBuilder.AppendLine($"// Warning: Node '{instance.NodeName}' template missing.");
            }

            currentNodeId = instance.NextExecutionNodeId;
        }

        return codeBuilder.ToString();
    }
}
