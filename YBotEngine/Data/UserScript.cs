using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NetCord;

namespace YBotEngine.Data;

public enum ScriptTriggerType
{
    GatewayEvent = 0,
    TextCommand = 1,
    SlashCommand = 2
}

public class UserScript
{
    [Key]
    public string Id { get; set; } = string.Empty;
    
    [Required] 
    public string Name { get; set; } = string.Empty; 
    
    [Required]
    public ScriptTriggerType TriggerType { get; set; } = ScriptTriggerType.GatewayEvent;

    // Examples: "MessageCreated" | "ping" | "system:status" | "database:scripts:count"
    [Required] 
    public string TriggerKey { get; set; } = string.Empty; 

    [Required] 
    public string CompilerType { get; set; } = "csharp";
    
    [Required] 
    public string Body { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Permissions? RequiredGuildPermissions { get; set; }

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public ICollection<CommandOption> Options { get; set; } = new List<CommandOption>();
}

public class CommandOption
{
    [Key]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string UserScriptId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserScriptId))]
    public UserScript? ParentScript { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public ApplicationCommandOptionType Type { get; set; } = ApplicationCommandOptionType.String;

    [Required]
    public bool IsRequired { get; set; } = false;
}