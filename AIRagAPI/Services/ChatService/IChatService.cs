using AIRagAPI.Services.DTOs;

namespace AIRagAPI.Services.ChatService;

public interface IChatService
{
    Task<ChatMessageResponse> SendMessage(Guid userId, ChatRequest request, CancellationToken cancellationToken);
    
    Task<List<ConversationResponse>> GetUserAllConversions(Guid userId, CancellationToken cancellationToken);
    Task<List<ChatMessageResponse>?> GetUserAllConversationMessages(Guid userId, Guid? conversationId, CancellationToken cancellationToken);
    
    // Task<ConversationResponse> CreateConversation(string message, Guid userId, CancellationToken cancellationToken);
}