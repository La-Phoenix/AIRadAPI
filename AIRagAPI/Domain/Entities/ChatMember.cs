namespace AIRagAPI.Domain.Entities;

public class ChatMember : BaseEntity
{
    public Guid ChatId { get; set; }
    public Chat Chat { get; set; } = null!;
    
    // Null if member is the Assistant (system)
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    
    // Display name (for Assistant or custom names)
    public required string DisplayName { get; set; }
    
    // Whether this member is the AI Assistant (for system-like behavior)
    public bool IsAssistant { get; set; } = false;

    // Read receipt state for this member in this chat
    public int LastReadOrder { get; set; } = -1;
    public DateTime? LastReadAt { get; set; }
    
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
