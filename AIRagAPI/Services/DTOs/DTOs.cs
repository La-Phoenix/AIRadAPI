using System.ComponentModel.DataAnnotations;

namespace AIRagAPI.Services.DTOs;

public class Response<T>
{
    public required string Message {get; set;}
    public T? Data {get; set;} 
    public bool IsSuccess { get; set; } = false;
};

public record ChatRequest
{
    [Required(ErrorMessage = "Please Send a message")]
    public required string Question { get; init; }

    public bool IsNewConversation { get; init; } = false;
    public string? ConversationId { get; init; }
}

public record AddDocRequest
{
    [Required(ErrorMessage = "Document Text is required")]
    public required string Text { get; init; }
}

public record ChatResponse
{
    public required string Question { get; init; }
    public required string Message { get; init; }
}

public record UserResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public string? PictureUrl { get; init; }
}

public record ChatMessageResponse
{
    public required string Id { get; init; }
    public required string ConversationId { get; set; }
    public required string Role  { get; set; }
    public required string Content { get; set; }
    public required string Order { get; set; }
    public required string CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    // public required string UserMessageId { get; set; }
}

public record ConversationResponse
{
    public required string Id { get; set; }
    public required string UserId { get; set; }
    public required string Title { get; set; }
    public bool IsActive { get; set; }
    public int MessageCount { get; set; }
    public string? Summary { get; set; }
    public List<ChatMessageResponse> Messages { get; set; } = new List<ChatMessageResponse>();
    public required string CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}

// Real-time Chat DTOs
public record CreateDirectChatRequest
{
    public required Guid OtherUserId { get; init; }
    public bool WithAssistant { get; init; } = false;
}

public record CreateGroupChatRequest
{
    public required string Name { get; init; }
    public required List<Guid> MemberUserIds { get; init; }
}

public record CreateRagAssistantGroupRequest
{
    public required string Name { get; init; }
    public required List<Guid> MemberUserIds { get; init; }
}

public record ChatMemberDto
{
    public required Guid Id { get; init; }
    public required Guid ChatId { get; init; }
    public Guid? UserId { get; init; }
    public required string DisplayName { get; init; }
    public bool IsAssistant { get; init; }
    public bool IsOnline { get; init; }
    public int LastReadOrder { get; init; }
    public DateTime? LastReadAt { get; init; }
    public required DateTime JoinedAt { get; init; }
}

public record ChatMessageDto
{
    public required Guid Id { get; init; }
    public required Guid ChatId { get; init; }
    public required Guid SenderId { get; init; }
    public required string Content { get; init; }
    public required string Role { get; init; }
    public string? RetrievalContext { get; init; }
    public int Order { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? EditedAt { get; init; }
}

public record ChatDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Type { get; init; }
    public bool IsActive { get; init; }
    public int MessageCount { get; init; }
    public int CurrentUserLastReadOrder { get; init; }
    public int CurrentUserUnreadCount { get; init; }
    public required DateTime LastMessageAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public List<ChatMemberDto> Members { get; init; } = new();
}

public record SendMessageRequest
{
    [Required(ErrorMessage = "Message content is required")]
    public required string Content { get; init; }
    public string? RetrievalContext { get; init; }
}

public record UpdateChatRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
}