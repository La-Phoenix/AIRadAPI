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
    public required string Question { get; init; }
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