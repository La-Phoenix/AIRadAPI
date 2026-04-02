using AIRagAPI.Domain.Enums;

namespace AIRagAPI.Domain.Entities;

public class Message: BaseEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public MessageRole Role  { get; set; } = MessageRole.User;
    public required string Content { get; set; }
    public int Order { get; set; }
    public int? TokenCount { get; set; }
}