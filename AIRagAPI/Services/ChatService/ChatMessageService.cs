using AIRagAPI.Domain.Entities;
using AIRagAPI.Domain.Enums;
using AIRagAPI.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AIRagAPI.Services.ChatService;

public class ChatMessageService : IChatMessageService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ChatMessageService> _logger;

    public ChatMessageService(AppDbContext db, ILogger<ChatMessageService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ChatMessage> SendMessageAsync(Guid chatId, Guid senderId, string content, string? retrievalContext = null, CancellationToken cancellationToken = default)
    {
        var chat = await _db.Chats.FindAsync([chatId], cancellationToken);
        if (chat == null)
            throw new Exception("Chat not found");

        var sender = await _db.ChatMembers.FindAsync([senderId], cancellationToken);
        if (sender == null || sender.ChatId != chatId)
            throw new Exception("Sender is not a member of this chat");

        // Get next message order
        var maxOrder = await _db.ChatMessages
            .Where(m => m.ChatId == chatId)
            .MaxAsync(m => (int?)m.Order, cancellationToken) ?? -1;

        var message = new ChatMessage
        {
            ChatId = chatId,
            SenderId = senderId,
            Content = content,
            Role = sender.IsAssistant ? MessageRole.Assistant : MessageRole.User,
            RetrievalContext = retrievalContext,
            Order = maxOrder + 1
        };

        _db.ChatMessages.Add(message);

        // Update chat
        chat.MessageCount++;
        chat.LastMessageAt = DateTime.UtcNow;
        _db.Chats.Update(chat);

        await _db.SaveChangesAsync(cancellationToken);

        return message;
    }

    public async Task<int> MarkMessagesReadAsync(Guid chatId, Guid userId, int upToOrder, CancellationToken cancellationToken = default)
    {
        var member = await _db.ChatMembers
            .FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == userId && cm.IsActive, cancellationToken);

        if (member == null)
            throw new Exception("Member not found");

        if (upToOrder < member.LastReadOrder)
            return member.LastReadOrder;

        member.LastReadOrder = upToOrder;
        member.LastReadAt = DateTime.UtcNow;
        _db.ChatMembers.Update(member);
        await _db.SaveChangesAsync(cancellationToken);

        return member.LastReadOrder;
    }

    public async Task<int> GetUnreadCountAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await _db.ChatMembers
            .FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == userId && cm.IsActive, cancellationToken);

        if (member == null)
            throw new Exception("Member not found");

        var unreadCount = await _db.ChatMessages
            .Where(m => m.ChatId == chatId && !m.IsDeleted && m.Order > member.LastReadOrder)
            .Where(m => m.Sender.UserId != userId)
            .CountAsync(cancellationToken);

        return unreadCount;
    }

    public async Task<int> GetLastReadOrderAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await _db.ChatMembers
            .FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == userId && cm.IsActive, cancellationToken);

        if (member == null)
            throw new Exception("Member not found");

        return member.LastReadOrder;
    }

    public async Task<List<ChatMessage>> GetChatMessagesAsync(Guid chatId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
    {
        return await _db.ChatMessages
            .Where(m => m.ChatId == chatId && !m.IsDeleted)
            .OrderBy(m => m.Order)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<ChatMessage?> GetMessageByIdAsync(Guid messageId, CancellationToken cancellationToken)
    {
        return await _db.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == messageId && !m.IsDeleted, cancellationToken);
    }

    public async Task DeleteMessageAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await _db.ChatMessages.FindAsync([messageId], cancellationToken);
        if (message == null)
            throw new Exception("Message not found");

        message.IsDeleted = true;
        _db.ChatMessages.Update(message);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task EditMessageAsync(Guid messageId, string newContent, CancellationToken cancellationToken)
    {
        var message = await _db.ChatMessages.FindAsync([messageId], cancellationToken);
        if (message == null)
            throw new Exception("Message not found");

        message.Content = newContent;
        message.EditedAt = DateTime.UtcNow;
        _db.ChatMessages.Update(message);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
