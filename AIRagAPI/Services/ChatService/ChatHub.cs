using System.Text.Json;
using AIRagAPI.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AIRagAPI.Services.ChatService;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatManagementService _chatService;
    private readonly IChatMessageService _messageService;
    private readonly ChatPresenceTracker _presenceTracker;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IChatManagementService chatService,
        IChatMessageService messageService,
        ChatPresenceTracker presenceTracker,
        ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _messageService = messageService;
        _presenceTracker = presenceTracker;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation($"Client connected: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var changes = _presenceTracker.RemoveConnection(Context.ConnectionId);
        foreach (var change in changes.Where(c => c.becameOffline))
        {
            await Clients.Group(change.chatId.ToString()).SendAsync("presence_changed", new
            {
                chatId = change.chatId.ToString(),
                userId = change.userId.ToString(),
                isOnline = false
            });
        }

        _logger.LogInformation($"Client disconnected: {Context.ConnectionId}");
        if (exception != null)
            _logger.LogError($"Disconnection error: {exception.Message}");
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinChat(string chatId)
    {
        if (!Guid.TryParse(chatId, out var chatGuid))
        {
            await Clients.Caller.SendAsync("error", "Invalid chat ID");
            return;
        }

        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            await Clients.Caller.SendAsync("error", "Not authenticated");
            return;
        }

        try
        {
            // Verify user is a member of the chat
            var member = await _chatService.GetMemberAsync(chatGuid, userId, CancellationToken.None);
            if (member == null)
            {
                await Clients.Caller.SendAsync("error", "Not a member of this chat");
                return;
            }

            // Add to group named by chatId
            await Groups.AddToGroupAsync(Context.ConnectionId, chatId);

            _presenceTracker.AddConnectionToChat(Context.ConnectionId, chatGuid, userId, out var becameOnline);

            if (becameOnline)
            {
                await Clients.Group(chatId).SendAsync("presence_changed", new
                {
                    chatId,
                    userId = userId.ToString(),
                    isOnline = true
                });
            }

            var unreadCount = await _messageService.GetUnreadCountAsync(chatGuid, userId, CancellationToken.None);
            var lastReadOrder = await _messageService.GetLastReadOrderAsync(chatGuid, userId, CancellationToken.None);
            await Clients.Caller.SendAsync("read_state_synced", new
            {
                chatId,
                unreadCount,
                lastReadOrder
            });

            // Notify others that user joined
            await Clients.Group(chatId).SendAsync("user_joined", new
            {
                userId = userId.ToString(),
                connectionId = Context.ConnectionId,
                displayName = member.DisplayName
            });

            _logger.LogInformation($"User {userId} joined chat {chatId}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error joining chat: {ex.Message}");
            await Clients.Caller.SendAsync("error", "Failed to join chat");
        }
    }

    public async Task LeaveChat(string chatId)
    {
        if (!Guid.TryParse(chatId, out var chatGuid))
            return;

        var userId = GetUserId();
        try
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId);

            if (_presenceTracker.RemoveConnectionFromChat(Context.ConnectionId, chatGuid, out var removedUserId, out var becameOffline))
            {
                if (becameOffline)
                {
                    await Clients.Group(chatId).SendAsync("presence_changed", new
                    {
                        chatId,
                        userId = removedUserId.ToString(),
                        isOnline = false
                    });
                }

                var member = await _chatService.GetMemberAsync(chatGuid, removedUserId, CancellationToken.None);
                await Clients.Group(chatId).SendAsync("user_left", new
                {
                    userId = removedUserId.ToString(),
                    connectionId = Context.ConnectionId,
                    displayName = member?.DisplayName ?? "Unknown"
                });
            }

            _logger.LogInformation($"User {userId} left chat {chatId}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error leaving chat: {ex.Message}");
        }
    }

    public async Task SendMessage(object payloadOrChatId, string? content = null)
    {
        var (chatId, resolvedContent) = ParseSendMessagePayload(payloadOrChatId, content);

        _logger.LogInformation("Sending message: {ChatId}", chatId);
        if (!Guid.TryParse(chatId, out var chatGuid))
        {
            _logger.LogError("Invalid chat ID: {ChatId}", chatId);
            throw new HubException("Invalid chat ID");
        }

        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            throw new HubException("Not authenticated");
        }

        if (string.IsNullOrWhiteSpace(resolvedContent))
        {
            _logger.LogError("Empty message");
            throw new HubException("Message cannot be empty");
        }

        try
        {
            // Get sender (ChatMember)
            var member = await _chatService.GetMemberAsync(chatGuid, userId, CancellationToken.None);
            if (member == null)
            {
                _logger.LogWarning($"User {userId} is not a member of chat {chatGuid}");
                throw new HubException("Not a member of this chat");
            }

            // Save message to database
            var message = await _messageService.SendMessageAsync(
                chatGuid,
                member.Id,
                resolvedContent);

            // Broadcast to all connected clients in the chat
            var response = new
            {
                type = "message",
                chatId,
                messageId = message.Id.ToString(),
                senderId = userId.ToString(),
                senderName = member.DisplayName,
                content = message.Content,
                role = message.Role.ToString(),
                createdAt = message.CreatedAt,
                order = message.Order
            };

            await Clients.Group(chatId).SendAsync("receive_message", response);

            var members = await _chatService.GetChatMembersAsync(chatGuid, CancellationToken.None);
            foreach (var chatMember in members.Where(m => m.UserId.HasValue && m.UserId.Value != userId))
            {
                var targetUserId = chatMember.UserId!.Value;
                var unreadCount = await _messageService.GetUnreadCountAsync(chatGuid, targetUserId, CancellationToken.None);

                await Clients.Group(chatId).SendAsync("unread_count_changed", new
                {
                    chatId,
                    userId = targetUserId.ToString(),
                    unreadCount
                });
            }

            // If it's a RAG assistant group and message is from user, trigger assistant response
            var chat = await _chatService.GetChatByIdAsync(chatGuid, CancellationToken.None);
            if (chat?.Type == ChatType.RagAssistantGroup && !member.IsAssistant)
            {
                _logger.LogInformation($"RAG Assistant should respond to message in chat {chatGuid}");
                // This could trigger a background task to call the RAG pipeline
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling chat message for chat {ChatId} and user {UserId}", chatId, userId);
            throw new HubException($"Failed to send message: {ex.Message}");
        }
    }

    public async Task MarkAsRead(object payloadOrChatId, int? upToOrder = null)
    {
        var (chatId, lastReadOrder) = ParseMarkReadPayload(payloadOrChatId, upToOrder);

        if (!Guid.TryParse(chatId, out var chatGuid))
            throw new HubException("Invalid chat ID");

        var userId = GetUserId();
        if (userId == Guid.Empty)
            throw new HubException("Not authenticated");

        if (lastReadOrder < 0)
            throw new HubException("Invalid lastReadOrder");

        var member = await _chatService.GetMemberAsync(chatGuid, userId, CancellationToken.None);
        if (member == null)
            throw new HubException("Not a member of this chat");

        var updatedLastReadOrder = await _messageService.MarkMessagesReadAsync(
            chatGuid,
            userId,
            lastReadOrder,
            CancellationToken.None);

        var unreadCount = await _messageService.GetUnreadCountAsync(chatGuid, userId, CancellationToken.None);

        await Clients.Caller.SendAsync("read_state_synced", new
        {
            chatId,
            unreadCount,
            lastReadOrder = updatedLastReadOrder
        });

        await Clients.GroupExcept(chatId, Context.ConnectionId).SendAsync("message_read", new
        {
            chatId,
            readerUserId = userId.ToString(),
            lastReadOrder = updatedLastReadOrder,
            readAt = DateTime.UtcNow
        });

        await Clients.Group(chatId).SendAsync("unread_count_changed", new
        {
            chatId,
            userId = userId.ToString(),
            unreadCount
        });
    }

    private static (string chatId, int lastReadOrder) ParseMarkReadPayload(object payloadOrChatId, int? upToOrder)
    {
        if (payloadOrChatId is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Object)
            {
                var chatIdValue = jsonElement.TryGetProperty("chatId", out var chatIdElement)
                    ? chatIdElement.GetString()
                    : null;

                var orderValue = jsonElement.TryGetProperty("lastReadOrder", out var orderElement) &&
                                 orderElement.ValueKind == JsonValueKind.Number
                    ? orderElement.GetInt32()
                    : -1;

                return (chatIdValue ?? string.Empty, orderValue);
            }

            if (jsonElement.ValueKind == JsonValueKind.String)
            {
                return (jsonElement.GetString() ?? string.Empty, upToOrder ?? -1);
            }
        }

        return (payloadOrChatId?.ToString() ?? string.Empty, upToOrder ?? -1);
    }

    private static (string chatId, string content) ParseSendMessagePayload(object payloadOrChatId, string? content)
    {
        if (payloadOrChatId is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Object)
            {
                var chatIdValue = jsonElement.TryGetProperty("chatId", out var chatIdElement)
                    ? chatIdElement.GetString()
                    : null;

                var contentValue = jsonElement.TryGetProperty("content", out var contentElement)
                    ? contentElement.GetString()
                    : null;

                return (chatIdValue ?? string.Empty, contentValue ?? string.Empty);
            }

            if (jsonElement.ValueKind == JsonValueKind.String)
            {
                return (jsonElement.GetString() ?? string.Empty, content ?? string.Empty);
            }
        }

        var chatId = payloadOrChatId?.ToString() ?? string.Empty;
        return (chatId, content ?? string.Empty);
    }

    public async Task SendTypingIndicator(string chatId, bool isTyping)
    {
        if (!Guid.TryParse(chatId, out var chatGuid))
        {
            throw new HubException("Invalid chat ID");
        }

        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            throw new HubException("Not authenticated");
        }

        try
        {
            var member = await _chatService.GetMemberAsync(chatGuid, userId, CancellationToken.None);
            if (member == null)
            {
                throw new HubException("Not a member of this chat");
            }

            var response = new
            {
                type = "typing",
                chatId,
                userId = userId.ToString(),
                isTyping = isTyping
            };

            // Send to all except the sender
            await Clients.GroupExcept(chatId, Context.ConnectionId).SendAsync("user_typing", response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending typing indicator for chat {ChatId} and user {UserId}", chatId, userId);
            throw new HubException($"Failed to send typing indicator: {ex.Message}");
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst("sub")?.Value 
            ?? Context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        
        if (Guid.TryParse(userIdClaim, out var userId))
            return userId;

        return Guid.Empty;
    }
}
