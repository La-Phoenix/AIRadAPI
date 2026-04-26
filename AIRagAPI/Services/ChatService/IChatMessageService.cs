using AIRagAPI.Domain.Entities;
using AIRagAPI.Domain.Enums;

namespace AIRagAPI.Services.ChatService;

public interface IChatMessageService
{
    Task<ChatMessage> SendMessageAsync(Guid chatId, Guid senderId, string content, string? retrievalContext = null, CancellationToken cancellationToken = default);
    Task<int> MarkMessagesReadAsync(Guid chatId, Guid userId, int upToOrder, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetLastReadOrderAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default);
    Task<List<ChatMessage>> GetChatMessagesAsync(Guid chatId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<ChatMessage?> GetMessageByIdAsync(Guid messageId, CancellationToken cancellationToken);
    Task DeleteMessageAsync(Guid messageId, CancellationToken cancellationToken);
    Task EditMessageAsync(Guid messageId, string newContent, CancellationToken cancellationToken);
}
