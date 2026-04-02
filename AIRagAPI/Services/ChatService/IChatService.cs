using AIRagAPI.Services.DTOs;

namespace AIRagAPI.Services.ChatService;

public interface IChatService
{
    Task<ChatMessageResponse> SendMessage(Guid userId, string message);
    
    Task<List<ConversationResponse>> GetUserAllConversions(Guid userId);
    Task<List<ChatMessageResponse>?> GetUserAllConversationMessages(Guid userId, Guid? conversationId);
}