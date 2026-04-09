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