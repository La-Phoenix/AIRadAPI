using AIRagAPI.Domain.Enums;

namespace AIRagAPI.Domain.Entities;

public class User: BaseEntity
{
    public required string Name { get; set; } 
    public required string Email { get; set; }
    public string? PictureUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public ICollection<Conversation>  Conversations { get; set; } = new List<Conversation>();
}