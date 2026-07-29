namespace YScriptEngine.NodeGraph;

public class GraphConfiguration
{
    public string StartNodeId { get; set; } = string.Empty;
    public Dictionary<string, NodeInstance> Nodes { get; set; } = new(StringComparer.Ordinal);
}

public class NodeInstance
{
    public string Id { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public Dictionary<string, string> PinVariableAssignments { get; set; } = new(StringComparer.Ordinal);
    public string NextExecutionNodeId { get; set; } = string.Empty;
}