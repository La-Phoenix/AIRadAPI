namespace AIRagAPI.Domain.Entities;

public class Conversation: BaseEntity
{
    public Guid UserId { get; set; }
    public required User User { get; set; }
    public required string Title { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}