using System.Security.Claims;
using AIRagAPI.Domain.Enums;
using AIRagAPI.Domain.Persistence;
using AIRagAPI.Services.ChatService;
using AIRagAPI.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIRagAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RealtimeChatController : ControllerBase
{
    private readonly IChatManagementService _chatService;
    private readonly IChatMessageService _messageService;
    private readonly ChatPresenceTracker _presenceTracker;
    private readonly ILogger<RealtimeChatController> _logger;
    private readonly AppDbContext _db;

    public RealtimeChatController(
        IChatManagementService chatService,
        IChatMessageService messageService,
        ChatPresenceTracker presenceTracker,
        ILogger<RealtimeChatController> logger,
        AppDbContext db)
    {
        _chatService = chatService;
        _messageService = messageService;
        _presenceTracker = presenceTracker;
        _logger = logger;
        _db = db;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(userIdClaim?.Value ?? throw new UnauthorizedAccessException());
    }

    // Create direct chat with another user
    [HttpPost("direct")]
    public async Task<IActionResult> CreateDirectChat([FromBody] CreateDirectChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            var chat = request.WithAssistant
                ? await _chatService.CreateDirectChatWithAssistantAsync(userId, cancellationToken)
                : await _chatService.CreateDirectChatAsync(userId, request.OtherUserId, cancellationToken);

            var chatDto = await MapChatToDto(chat, userId, cancellationToken);
            return Ok(new Response<ChatDto> { Message = "Chat created", Data = chatDto, IsSuccess = true });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating direct chat: {ex.Message}");
            return BadRequest(new Response<object> { Message = ex.Message });
        }
    }

    // Create group chat
    [HttpPost("group")]
    public async Task<IActionResult> CreateGroupChat([FromBody] CreateGroupChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            var chat = await _chatService.CreateGroupChatAsync(userId, request.Name, request.MemberUserIds, cancellationToken);

            var chatDto = await MapChatToDto(chat, userId, cancellationToken);
            return Ok(new Response<ChatDto> { Message = "Group chat created", Data = chatDto, IsSuccess = true });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating group chat: {ex.Message}");
            return BadRequest(new Response<object> { Message = ex.Message });
        }
    }

    // Create RAG assistant group
    [HttpPost("rag-group")]
    public async Task<IActionResult> CreateRagAssistantGroup([FromBody] CreateRagAssistantGroupRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            var chat = await _chatService.CreateRagAssistantGroupAsync(userId, request.Name, request.MemberUserIds, cancellationToken);

            var chatDto = await MapChatToDto(chat, userId, cancellationToken);
            return Ok(new Response<ChatDto> { Message = "RAG Assistant group created", Data = chatDto, IsSuccess = true });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating RAG group: {ex.Message}");
            return BadRequest(new Response<object> { Message = ex.Message });
        }
    }

    // Get all user's chats
    [HttpGet]
    public async Task<IActionResult> GetUserChats([FromQuery] string? typeFilter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetUserId();
            ChatType? typeEnum = null;

            if (!string.IsNullOrEmpty(typeFilter) && Enum.TryParse<ChatType>(typeFilter, out var parsed))
                typeEnum = parsed;

            var chats = await _chatService.GetUserChatsAsync(userId, typeEnum, cancellationToken);
            var chatDtos = new List<ChatDto>();

            foreach (var chat in chats)
            {
                chatDtos.Add(await MapChatToDto(chat, userId, cancellationToken));
            }

            return Ok(new Response<List<ChatDto>> { Message = "Chats retrieved", Data = chatDtos, IsSuccess = true });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving chats: {ex.Message}");
            return BadRequest(new Response<object> { Message = ex.Message });
        }
    }

    // Search chats by name or member
    [HttpGet("search")]
    public async Task<IActionResult> SearchChats([FromQuery] string query, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔵 SearchChats: {Query}", query);

            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(query))
            {
                // If no query, return all chats
                var allChats = await _chatService.GetUserChatsAsync(userId, cancellationToken: cancellationToken);
                var dtos = new List<ChatDto>();
                foreach (var chat in allChats)
                {
                    dtos.Add(await MapChatToDto(chat, userId, cancellationToken));
                }
                return Ok(new Response<List<ChatDto>> { Message = "All chats retrieved", Data = dtos, IsSuccess = true });
            }

            // Search by chat name or member name or email (case-insensitive)
            var userChats = await _chatService.GetUserChatsAsync(userId, cancellationToken: cancellationToken);
            var queryLower = query.ToLower();

            var searchResults = userChats
                .Where(c =>
                    // Search by chat name
                    c.Name.ToLower().Contains(queryLower) ||
                    // Search by member display names
                    c.Members.Any(m => m.DisplayName.ToLower().Contains(queryLower)) ||
                    // Search by member email
                    c.Members.Any(m => m.User != null && m.User.Email.ToLower().Contains(queryLower))
                )
                .ToList();

            var resultDtos = new List<ChatDto>();
            foreach (var chat in searchResults)
            {
                resultDtos.Add(await MapChatToDto(chat, userId, cancellationToken));
            }

            _logger.LogInformation("🔵 Found {Count} chats matching: '{Query}'", resultDtos.Count, query);
            return Ok(new Response<List<ChatDto>> { Message = $"Found {resultDtos.Count} chats", Data = resultDtos, IsSuccess = true });
        }
        catch (Exception ex)
        {
            _logger.LogError($"🔴 Search error: {ex.Message}");
            return BadRequest(new Response<object> { Message = ex.Message });
        }
    }

    // Search users to start direct chat
    [HttpGet("search-users")]
    public async Task<IActionResult> SearchUsers([FromQuery] string query, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔵 SearchUsers: {Query}", query);

            var currentUserId = GetUserId();

            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return BadRequest(new Response<object> { Message = "Query must be at least 2 characters" });
            }

            var queryLower = query.ToLower();

            // Search users by name or email (case-insensitive), excluding current user
            var users = await _db.Users
                .Where(u =>
                    u.Id != currentUserId && (
                        u.Name.ToLower().Contains(queryLower) ||
                        u.Email.ToLower().Contains(queryLower)
                    )
                )
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Email,
                    u.PictureUrl
                })
                .Take(20) // Limit to 20 results
                .ToListAsync(cancellationToken);

            _logger.LogInformation("🔵 Found {Count} users matching: '{Query}'", users.Count, query);

            var userDtos = users.Select(u => new
            {
                id = u.Id,
                name = u.Name,
                email = u.Email,
                pictureUrl = u.PictureUrl
            }).ToList();

            return Ok(new Response<object> { Message = $"Found {users.Count} users", Data = userDtos, IsSuccess = true });
        }
        catch (Exception ex)
        {
            _logger.LogError($"🔴 Search users error: {ex.Message}");
            return BadRequest(new Response<object> { Message = ex.Message });
        }
    }

    // Get chat by ID with messages
    [HttpGet("{chatId}")]
    public async Task<IActionResult> GetChat(Guid chatId, [FromQuery] int limit = 50, [FromQuery] int offset = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetUserId();
            var chat = await _chatService.GetChatByIdAsync(chatId, cancellationToken);

            if (chat == null)
                return NotFound(new Response<object> { Message = "Chat not found" });

            // Verify user is a member
            var member = await _chatService.GetMemberAsync(chatId, userId, cancellationToken);
            if (member == null)
                return Forbid();

            var messages = await _messageService.GetChatMessagesAsync(chatId, limit, offset, cancellationToken);
            var chatDto = await MapChatToDto(chat, userId, cancellationToken);
            chatDto = chatDto with { Members = await MapChatMembersToDto(chat.Members, cancellationToken) };

            var response = new { chat = chatDto, messages = messages.Select(MapMessageToDto) };
            return Ok(new Response<object> { Message = "Chat retrieved", Data = response, IsSuccess = true });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving chat: {ex.Message}");
            return BadRequest(new Response<object> { Message = ex.Message });
        }
    }

    // Get chat members
    [HttpGet("{chatId}/members")]
    public async Task<IActionResult> GetChatMembers(Guid chatId, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetUserId();

            // Verify user is a member
            var member = await _chatService.GetMemberAsync(chatId, userId, cancellationToken);
            if (member == null)
                return Forbid();

            var members = await _chatService.GetChatMembersAsync(chatId, cancellationToken);
            var memberDtos = await MapChatMembersToDto(members, cancellationToken);

            return Ok(new Response<List<ChatMemberDto>> { Message = "Members retrieved", Data = memberDtos, IsSuccess = true });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving members: {ex.Message}");
            return BadRequest(new Response<object> { Message = ex.Message });
        }
    }

    // Add member to group chat
    [HttpPost("{chatId}/members/{userId}")]
    public async Task<IActionResult> AddMember(Guid chatId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentUserId = GetUserId();
            var chat = await _chatService.GetChatByIdAsync(chatId, cancellationToken);

            if (chat == null || chat.Type == ChatType.DirectMessage)
                return BadRequest(new Response<object> { Message = "Cannot add members to direct messages" });

            // Verify current user is creator or admin
            if (chat.CreatedByUserId != currentUserId)
                return Forbid();

            await _chatService.AddMemberAsync(chatId, userId, cancellationToken);
            return Ok(new Response<object> { Message = "Member added", IsSuccess = true });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error adding member: {ex.Message}");
            return BadRequest(new Response<object> { Message = ex.Message });
        }
    }

    // Remove member from group chat
    [HttpDelete("{chatId}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(Guid chatId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentUserId = GetUserId();
            var chat = await _chatService.GetChatByIdAsync(chatId, cancellationToken);

            if (chat == null)
                return NotFound(new Response<object> { Message = "Chat not found" });

            // User can remove themselves, or creator can remove anyone
            if (userId != currentUserId && chat.CreatedByUserId != currentUserId)
                return Forbid();

            await _chatService.RemoveMemberAsync(chatId, userId, cancellationToken);
            return Ok(new Response<object> { Message = "Member removed", IsSuccess = true });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error removing member: {ex.Message}");
            return BadRequest(new Response<object> { Message = ex.Message });
        }
    }

    // Update chat
    [HttpPut("{chatId}")]
    public async Task<IActionResult> UpdateChat(Guid chatId, [FromBody] UpdateChatRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetUserId();
            var chat = await _chatService.GetChatByIdAsync(chatId, cancellationToken);

            if (chat == null)
                return NotFound(new Response<object> { Message = "Chat not found" });

            // Only creator can update
            if (chat.CreatedByUserId != userId)
                return Forbid();

            await _chatService.UpdateChatAsync(chatId, request.Name, request.Description, cancellationToken);
            var updated = await _chatService.GetChatByIdAsync(chatId, cancellationToken);
            var chatDto = await MapChatToDto(updated!, userId, cancellationToken);

            return Ok(new Response<ChatDto> { Message = "Chat updated", Data = chatDto, IsSuccess = true });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating chat: {ex.Message}");
            return BadRequest(new Response<object> { Message = ex.Message });
        }
    }

    // Helper methods
    private ChatMessageDto MapMessageToDto(Domain.Entities.ChatMessage message)
    {
        return new ChatMessageDto
        {
            Id = message.Id,
            ChatId = message.ChatId,
            SenderId = message.SenderId,
            Content = message.Content,
            Role = message.Role.ToString(),
            RetrievalContext = message.RetrievalContext,
            Order = message.Order,
            CreatedAt = message.CreatedAt,
            EditedAt = message.EditedAt
        };
    }

    private async Task<List<ChatMemberDto>> MapChatMembersToDto(ICollection<Domain.Entities.ChatMember> members, CancellationToken cancellationToken)
    {
        return members.Select(m => new ChatMemberDto
        {
            Id = m.Id,
            ChatId = m.ChatId,
            UserId = m.UserId,
            DisplayName = m.DisplayName,
            IsAssistant = m.IsAssistant,
            IsOnline = m.UserId.HasValue && _presenceTracker.IsUserOnline(m.ChatId, m.UserId.Value),
            LastReadOrder = m.LastReadOrder,
            LastReadAt = m.LastReadAt,
            JoinedAt = m.JoinedAt
        }).ToList();
    }

    private async Task<ChatDto> MapChatToDto(Domain.Entities.Chat chat, Guid currentUserId, CancellationToken cancellationToken)
    {
        var currentUserLastReadOrder = await _messageService.GetLastReadOrderAsync(chat.Id, currentUserId, cancellationToken);
        var currentUserUnreadCount = await _messageService.GetUnreadCountAsync(chat.Id, currentUserId, cancellationToken);

        return new ChatDto
        {
            Id = chat.Id,
            Name = chat.Name,
            Description = chat.Description,
            Type = chat.Type.ToString(),
            IsActive = chat.IsActive,
            MessageCount = chat.MessageCount,
            CurrentUserLastReadOrder = currentUserLastReadOrder,
            CurrentUserUnreadCount = currentUserUnreadCount,
            LastMessageAt = chat.LastMessageAt,
            CreatedAt = chat.CreatedAt,
            Members = await MapChatMembersToDto(chat.Members, cancellationToken)
        };
    }
}
