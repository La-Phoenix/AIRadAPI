using AIRagAPI.Domain.Entities;
using AIRagAPI.Domain.Enums;
using AIRagAPI.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AIRagAPI.Services.ChatService;

public class ChatManagementService : IChatManagementService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ChatManagementService> _logger;

    public ChatManagementService(AppDbContext db, ILogger<ChatManagementService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Chat> CreateDirectChatAsync(Guid userId1, Guid userId2, CancellationToken cancellationToken)
    {
        var user1 = await _db.Users.FindAsync([userId1], cancellationToken);
        var user2 = await _db.Users.FindAsync([userId2], cancellationToken);
        if (user1 == null || user2 == null)
            throw new Exception("One or both users not found");

        // Check if direct chat already exists
        var existing = await _db.Chats
            .Include(c => c.Members)
            .Where(c => c.Type == ChatType.DirectMessage)
            .FirstOrDefaultAsync(c =>
                c.Members.Count == 2 &&
                c.Members.Any(m => m.UserId == userId1) &&
                c.Members.Any(m => m.UserId == userId2),
                cancellationToken);

        if (existing != null)
            return existing;

        var chat = new Chat
        {
            Name = $"Direct: {user1.Name} & {user2.Name}",
            Type = ChatType.DirectMessage,
            CreatedByUserId = userId1,
            LastMessageAt = DateTime.UtcNow
        };

        _db.Chats.Add(chat);
        await _db.SaveChangesAsync(cancellationToken);

        // Add members
        var member1 = new ChatMember { ChatId = chat.Id, UserId = userId1, DisplayName = user1.Name };
        var member2 = new ChatMember { ChatId = chat.Id, UserId = userId2, DisplayName = user2.Name };

        _db.ChatMembers.AddRange(member1, member2);
        await _db.SaveChangesAsync(cancellationToken);

        return chat;
    }

    public async Task<Chat> CreateDirectChatWithAssistantAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FindAsync([userId], cancellationToken);
        if (user == null)
            throw new Exception("User not found");

        // Check if direct chat with assistant already exists
        var existing = await _db.Chats
            .Include(c => c.Members)
            .Where(c => c.Type == ChatType.DirectMessage)
            .FirstOrDefaultAsync(c =>
                c.Members.Count == 2 &&
                c.Members.Any(m => m.UserId == userId) &&
                c.Members.Any(m => m.IsAssistant),
                cancellationToken);

        if (existing != null)
            return existing;

        var chat = new Chat
        {
            Name = $"Direct: {user.Name} & Assistant",
            Type = ChatType.DirectMessage,
            CreatedByUserId = userId,
            LastMessageAt = DateTime.UtcNow
        };

        _db.Chats.Add(chat);
        await _db.SaveChangesAsync(cancellationToken);

        var userMember = new ChatMember { ChatId = chat.Id, UserId = userId, DisplayName = user.Name };
        var assistantMember = new ChatMember { ChatId = chat.Id, DisplayName = "Assistant", IsAssistant = true };

        _db.ChatMembers.AddRange(userMember, assistantMember);
        await _db.SaveChangesAsync(cancellationToken);

        return chat;
    }

    public async Task<Chat> CreateGroupChatAsync(Guid creatorId, string name, List<Guid> memberUserIds, CancellationToken cancellationToken)
    {
        var creator = await _db.Users.FindAsync([creatorId], cancellationToken);
        if (creator == null)
            throw new Exception("Creator not found");

        var users = await _db.Users
            .Where(u => memberUserIds.Contains(u.Id) || u.Id == creatorId)
            .ToListAsync(cancellationToken);

        if (users.Count != memberUserIds.Count + 1)
            throw new Exception("One or more users not found");

        var chat = new Chat
        {
            Name = name,
            Type = ChatType.GroupChat,
            CreatedByUserId = creatorId,
            LastMessageAt = DateTime.UtcNow
        };

        _db.Chats.Add(chat);
        await _db.SaveChangesAsync(cancellationToken);

        var members = users.Select(u => new ChatMember
        {
            ChatId = chat.Id,
            UserId = u.Id,
            DisplayName = u.Name
        }).ToList();

        _db.ChatMembers.AddRange(members);
        await _db.SaveChangesAsync(cancellationToken);

        return chat;
    }

    public async Task<Chat> CreateRagAssistantGroupAsync(Guid creatorId, string name, List<Guid> memberUserIds, CancellationToken cancellationToken)
    {
        var creator = await _db.Users.FindAsync([creatorId], cancellationToken);
        if (creator == null)
            throw new Exception("Creator not found");

        var users = await _db.Users
            .Where(u => memberUserIds.Contains(u.Id) || u.Id == creatorId)
            .ToListAsync(cancellationToken);

        if (users.Count != memberUserIds.Count + 1)
            throw new Exception("One or more users not found");

        var chat = new Chat
        {
            Name = name,
            Type = ChatType.RagAssistantGroup,
            CreatedByUserId = creatorId,
            LastMessageAt = DateTime.UtcNow
        };

        _db.Chats.Add(chat);
        await _db.SaveChangesAsync(cancellationToken);

        var members = users.Select(u => new ChatMember
        {
            ChatId = chat.Id,
            UserId = u.Id,
            DisplayName = u.Name
        }).ToList();

        // Always add assistant to RAG groups
        members.Add(new ChatMember
        {
            ChatId = chat.Id,
            DisplayName = "RAG Assistant",
            IsAssistant = true
        });

        _db.ChatMembers.AddRange(members);
        await _db.SaveChangesAsync(cancellationToken);

        return chat;
    }

    public async Task<Chat?> GetChatByIdAsync(Guid chatId, CancellationToken cancellationToken)
    {
        return await _db.Chats
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == chatId, cancellationToken);
    }

    public async Task<List<Chat>> GetUserChatsAsync(Guid userId, ChatType? typeFilter = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Chats
            .Include(c => c.Members)
            .ThenInclude(m => m.User)
            .Where(c => c.Members.Any(m => m.UserId == userId));

        if (typeFilter.HasValue)
            query = query.Where(c => c.Type == typeFilter.Value);

        return await query
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ChatMember>> GetChatMembersAsync(Guid chatId, CancellationToken cancellationToken)
    {
        return await _db.ChatMembers
            .Where(cm => cm.ChatId == chatId && cm.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task AddMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken)
    {
        var chat = await _db.Chats.FindAsync([chatId], cancellationToken);
        if (chat == null)
            throw new Exception("Chat not found");

        var user = await _db.Users.FindAsync([userId], cancellationToken);
        if (user == null)
            throw new Exception("User not found");

        var existing = await _db.ChatMembers
            .FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == userId, cancellationToken);

        if (existing != null)
        {
            existing.IsActive = true;
            _db.ChatMembers.Update(existing);
        }
        else
        {
            var member = new ChatMember { ChatId = chatId, UserId = userId, DisplayName = user.Name };
            _db.ChatMembers.Add(member);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken)
    {
        var member = await _db.ChatMembers
            .FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == userId, cancellationToken);

        if (member == null)
            throw new Exception("Member not found");

        member.IsActive = false;
        _db.ChatMembers.Update(member);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ChatMember?> GetMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken)
    {
        return await _db.ChatMembers
            .FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == userId, cancellationToken);
    }

    public async Task UpdateChatAsync(Guid chatId, string name, string? description, CancellationToken cancellationToken)
    {
        var chat = await _db.Chats.FindAsync([chatId], cancellationToken);
        if (chat == null)
            throw new Exception("Chat not found");

        chat.Name = name;
        chat.Description = description;
        _db.Chats.Update(chat);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteChatAsync(Guid chatId, CancellationToken cancellationToken)
    {
        var chat = await _db.Chats.FindAsync([chatId], cancellationToken);
        if (chat == null)
            throw new Exception("Chat not found");

        chat.IsActive = false;
        _db.Chats.Update(chat);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
