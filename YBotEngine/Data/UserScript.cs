using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

    // For Events: "MessageCreated"
    // For Top Commands: "config"
    // For Subcommands: "prefix"
    [Required] 
    public string TriggerKey { get; set; } = string.Empty; 

    // SUBCOMMAND HIERARCHY HOOK
    // If null: This is a standalone command or an event trigger.
    // If set: This script is a subcommand belonging to a parent command.
    public string? ParentScriptId { get; set; }

    [ForeignKey(nameof(ParentScriptId))]
    public UserScript? ParentScript { get; set; }

    // Navigation property to easily grab nested items if needed
    public ICollection<UserScript> Subcommands { get; set; } = new List<UserScript>();

    public string? Description { get; set; }

    [Required] 
    public CompilerType CompilerType { get; set; } = CompilerType.Roslyn;
    
    [Required] 
    public string Body { get; set; } = string.Empty;
    
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}