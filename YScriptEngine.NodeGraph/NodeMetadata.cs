namespace YScriptEngine.NodeGraph;

public class NodeMetadata
{
    public string NodeName { get; set; } = string.Empty;
    public string FullTypeName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
    public bool IsStatic { get; set; }
    public List<PinDefinition> Inputs { get; set; } = new();
    public List<PinDefinition> Outputs { get; set; } = new();
    public Func<Dictionary<string, string>, string> CodeTemplate { get; set; } = _ => string.Empty;
}

public record PinDefinition(string Name, Type DataType);
