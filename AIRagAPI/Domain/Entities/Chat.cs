using AIRagAPI.Domain.Enums;

namespace AIRagAPI.Domain.Entities;

public class Chat : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ChatType Type { get; set; }
    public bool IsActive { get; set; } = true;
    
    // For DirectMessage: one user creates it, the other is a member
    public Guid? CreatedByUserId { get; set; }
    
    // For RagAssistantGroup: reference to context/documents if needed
    public string? Context { get; set; }
    
    public int MessageCount { get; set; } = 0;
    public DateTime LastMessageAt { get; set; }
    
    // Navigation
    public ICollection<ChatMember> Members { get; set; } = new List<ChatMember>();
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
