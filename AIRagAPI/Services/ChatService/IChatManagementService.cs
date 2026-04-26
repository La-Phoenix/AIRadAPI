using AIRagAPI.Domain.Entities;
using AIRagAPI.Domain.Enums;

namespace AIRagAPI.Services.ChatService;

public interface IChatManagementService
{
    // Create chats
    Task<Chat> CreateDirectChatAsync(Guid userId1, Guid userId2, CancellationToken cancellationToken);
    Task<Chat> CreateDirectChatWithAssistantAsync(Guid userId, CancellationToken cancellationToken);
    Task<Chat> CreateGroupChatAsync(Guid creatorId, string name, List<Guid> memberUserIds, CancellationToken cancellationToken);
    Task<Chat> CreateRagAssistantGroupAsync(Guid creatorId, string name, List<Guid> memberUserIds, CancellationToken cancellationToken);
    
    // Get chats
    Task<Chat?> GetChatByIdAsync(Guid chatId, CancellationToken cancellationToken);
    Task<List<Chat>> GetUserChatsAsync(Guid userId, ChatType? typeFilter = null, CancellationToken cancellationToken = default);
    Task<List<ChatMember>> GetChatMembersAsync(Guid chatId, CancellationToken cancellationToken);
    
    // Manage members
    Task AddMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken);
    Task RemoveMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken);
    Task<ChatMember?> GetMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken);
    
    // Update chat
    Task UpdateChatAsync(Guid chatId, string name, string? description, CancellationToken cancellationToken);
    Task DeleteChatAsync(Guid chatId, CancellationToken cancellationToken);
}
