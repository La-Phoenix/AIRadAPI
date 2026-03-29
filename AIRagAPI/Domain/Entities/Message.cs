using AIRagAPI.Domain.Enums;

namespace AIRagAPI.Domain.Entities;

public class Message: BaseEntity
{
    public Guid ConversationId { get; set; }
    public required Conversation Conversation { get; set; }
    public UserRole Role  { get; set; } = UserRole.User;
    public required string Content { get; set; }
}