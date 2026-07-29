namespace YScriptEngine.Abstractions;

public interface IScript
{
    Task ExecuteAsync(IScriptContext context);
}
