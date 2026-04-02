namespace AIRagAPI.Domain.Entities;

public class Conversation: BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Title { get; set; }
    public bool IsActive { get; set; } = true;
    public int MessageCount { get; set; } = 0;
    public string? Summary { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}