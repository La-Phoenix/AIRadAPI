using AIRagAPI.Domain.Enums;

namespace AIRagAPI.Domain.Entities;

public class ChatMessage : BaseEntity
{
    public Guid ChatId { get; set; }
    public Chat Chat { get; set; } = null!;
    
    public Guid SenderId { get; set; }
    public ChatMember Sender { get; set; } = null!;
    
    public required string Content { get; set; }
    public MessageRole Role { get; set; } = MessageRole.User;
    
    // Optional: context from RAG retrieval for assistant responses
    public string? RetrievalContext { get; set; }
    
    // For tracking if edited or deleted
    public bool IsDeleted { get; set; } = false;
    public DateTime? EditedAt { get; set; }
    
    public int Order { get; set; }
}
