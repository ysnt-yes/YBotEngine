using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace YScriptEngine.NodeGraph;

public interface INodeRegistry
{
    void RegisterType(Type type);
    Dictionary<string, List<NodeMetadata>> GetCatalog();
}

public class NodeRegistry : INodeRegistry
{
    private readonly ConcurrentDictionary<string, List<NodeMetadata>> _registry = new();
    
    public void RegisterType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.IsGenericTypeDefinition || type.IsSpecialName) return;

        var cleanFullName = CleanFullNameForUi(type.FullName ?? type.Name);
        var nodesForThisType = new List<NodeMetadata>();

        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        foreach (var ctor in constructors)
        {
            var node = CreateBaseMetadata(type, cleanFullName, $"New {type.Name}");
            node.Outputs.Add(new PinDefinition("Instance", type ));

            foreach (var p in ctor.GetParameters())
                node.Inputs.Add(new PinDefinition(p.Name!, p.ParameterType ));

            node.CodeTemplate = (pins) => {
                var args = string.Join(", ", ctor.GetParameters().Select(p => pins[p.Name!]));
                return $"var {pins["Instance"]} = new {type.FullName}({args});";
            };
            nodesForThisType.Add(node);
        }

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        foreach (var method in methods)
        {
            if (method.IsSpecialName) continue;

            var node = CreateBaseMetadata(type, cleanFullName, $"{type.Name}.{method.Name}");
            node.IsStatic = method.IsStatic;

            if (!method.IsStatic)
            {
                node.Inputs.Add(new PinDefinition("Target Instance", type ));
            }

            foreach (var param in method.GetParameters())
            {
                node.Inputs.Add(new PinDefinition(param.Name!, param.ParameterType ));
            }

            ProcessMethodOutputs(method, type, node);
            nodesForThisType.Add(node);
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        foreach (var prop in properties)
        {
            if (prop.CanRead)
            {
                var isStatic = prop.GetMethod?.IsStatic ?? false;
                var getNode = CreateBaseMetadata(type, cleanFullName, $"Get {type.Name}.{prop.Name}");
                getNode.IsStatic = isStatic;

                if (!isStatic) getNode.Inputs.Add(new PinDefinition("Target Instance", type ));
                getNode.Outputs.Add(new PinDefinition("Value", prop.PropertyType ));

                getNode.CodeTemplate = (pins) => {
                    var target = isStatic ? type.FullName : pins["Target Instance"];
                    return $"var {pins["Value"]} = {target}.{prop.Name};";
                };
                nodesForThisType.Add(getNode);
            }

            if (!prop.CanWrite) continue;
            {
                var isStatic = prop.SetMethod?.IsStatic ?? false;
                var setNode = CreateBaseMetadata(type, cleanFullName, $"Set {type.Name}.{prop.Name}");
                setNode.IsStatic = isStatic;

                if (!isStatic) setNode.Inputs.Add(new PinDefinition("Target Instance", type ));
                setNode.Inputs.Add(new PinDefinition("New Value", prop.PropertyType ));

                setNode.CodeTemplate = (pins) => {
                    var target = isStatic ? type.FullName : pins["Target Instance"];
                    return $"{target}.{prop.Name} = {pins["New Value"]};";
                };
                nodesForThisType.Add(setNode);
            }
        }

        _registry.AddOrUpdate(cleanFullName, nodesForThisType, (_, _) => nodesForThisType);
    }

    public Dictionary<string, List<NodeMetadata>> GetCatalog() 
        => _registry.ToDictionary(k => k.Key, v => v.Value);

    private static NodeMetadata CreateBaseMetadata(Type type, string cleanFullName, string displayName)
    {
        return new NodeMetadata {
            NodeName = displayName,
            FullTypeName = cleanFullName,
            Namespace = type.Namespace ?? string.Empty,
            SubCategory = type.Name.Replace("`1", string.Empty)
        };
    }

    private static void ProcessMethodOutputs(MethodInfo method, Type type, NodeMetadata node)
    {
        var returnType = method.ReturnType;
        var isAsync = typeof(Task).IsAssignableFrom(returnType);
        var awaitKeyword = isAsync ? "await " : string.Empty;

        if (isAsync && returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            returnType = returnType.GetGenericArguments()[0];
        }

        if (returnType is { IsValueType: true, FullName: not null } && returnType.FullName.StartsWith("System.ValueTuple"))
        {
            var fields = returnType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var elementNamesAttribute = method.ReturnParameter.GetCustomAttribute<TupleElementNamesAttribute>();

            for (var i = 0; i < fields.Length; i++)
            {
                var pinName = elementNamesAttribute?.TransformNames[i] ?? fields[i].Name;
                node.Outputs.Add(new PinDefinition(pinName, fields[i].FieldType));
            }

            node.CodeTemplate = (pins) => {
                var outVars = string.Join(", ", node.Outputs.Select(o => pins[o.Name]));
                var args = string.Join(", ", method.GetParameters().Select(p => pins[p.Name!]));
                var invocation = method.IsStatic ? $"{type.FullName}.{method.Name}({args})" : $"{pins["Target Instance"]}.{method.Name}({args})";
                return $"var ({outVars}) = {awaitKeyword}{invocation};";
            };
        }
        else if (returnType != typeof(void) && returnType != typeof(Task))
        {
            node.Outputs.Add(new PinDefinition("Result", returnType));

            node.CodeTemplate = (pins) => {
                var args = string.Join(", ", method.GetParameters().Select(p => pins[p.Name!]));
                var invocation = method.IsStatic ? $"{type.FullName}.{method.Name}({args})" : $"{pins["Target Instance"]}.{method.Name}({args})";
                return $"var {pins["Result"]} = {awaitKeyword}{invocation};";
            };
        }
        else
        {
            node.CodeTemplate = (pins) => {
                var args = string.Join(", ", method.GetParameters().Select(p => pins[p.Name!]));
                var invocation = method.IsStatic ? $"{type.FullName}.{method.Name}({args})" : $"{pins["Target Instance"]}.{method.Name}({args})";
                return $"{awaitKeyword}{invocation};";
            };
        }
    }

    private static string CleanFullNameForUi(string fullName)
    {
        var clean = fullName.Replace('+', '.');
        var backtickIndex = clean.IndexOf('`');
        return backtickIndex > 0 ? clean[..backtickIndex] : clean;
    }
}
