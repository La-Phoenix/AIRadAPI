namespace AIRagAPI.Domain.Entities;

public class User: BaseEntity
{
    public required string Name { get; set; } 
    public required string Email { get; set; }
    public string? PictureUrl { get; set; }
    public ICollection<Conversation>  Conversations { get; set; } = new List<Conversation>();
}