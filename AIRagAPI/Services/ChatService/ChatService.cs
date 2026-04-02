using System.Globalization;
using AIRagAPI.Common;
using AIRagAPI.Domain.Entities;
using AIRagAPI.Domain.Enums;
using AIRagAPI.Domain.Persistence;
using AIRagAPI.Services.Agents;
using AIRagAPI.Services.DTOs;
using AIRagAPI.Services.Vector;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AIRagAPI.Services.ChatService;

public class ChatService: IChatService
{
    private readonly AppDbContext _db;
    private readonly ChatSettings _settings;
    private readonly ILogger<ChatService> _logger;
    private readonly AgentCoordinator _agentCoordinator;

    public ChatService(AppDbContext dbContext, AgentCoordinator agentCoordinator, ILogger<ChatService> logger,IOptions<ChatSettings> settings)
    {
        _db = dbContext;
        _settings = settings.Value;
        _agentCoordinator = agentCoordinator;
        _logger = logger;
    }

    public async Task<ChatMessageResponse> SendMessage(Guid userId, string message)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) throw new Exception("User not found");
        // Get active conversation
        var conversation = await _db.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.UserId == userId && c.IsActive);
        
        // Create a new one if none exist. If above message limit disable conversion
        if (conversation == null || conversation.MessageCount >= _settings.MaxMessages)
        {
            if (conversation != null) conversation.IsActive = false;
            conversation = new Conversation
            {
                Title = "New Chat",
                UserId = userId,
                IsActive = true,
            };
            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync();
        }

        // Message order
        int order = conversation.MessageCount + 1;
        
        // User message - first msg
        var userMessage = new Message
        {
            ConversationId = conversation.Id,
            Order = order,
            Content = message,
            Role = MapToMessageRole(user.Role)
        };
        _db.Messages.Add(userMessage);
        conversation.MessageCount ++;
        
        // Build short term memory history - Order conversation messages take chat context messages
        var historyMsgs = conversation.Messages
            .OrderBy(m => m.Order)
            .TakeLast(_settings.ContextWindow)
            .Select(m => $"{m.Role.ToString()}: {m.Content}").ToList();
        
        // Combine history into the question
        var enrichedQuestion = $@"
        Previous conversation:
        {string.Join(Environment.NewLine, historyMsgs)}

        Current question: 
        {message}
        ";
        
        // Run Agent pipeline (RAG + LLM)
        var response = await _agentCoordinator.AskAsync(enrichedQuestion);
        
        // Save AI response - next msg
        var aiMessage = new Message
        {
            Conversation = conversation,
            Order = order + 1,
            Content = response,
            Role = MessageRole.Assistant
        };
        _db.Messages.Add(aiMessage);
        conversation.MessageCount ++;
        
        await _db.SaveChangesAsync();
        
        return new ChatMessageResponse
        {
            Id = userMessage.Id.ToString(),
            Role = userMessage.Role.ToString(),
            Content = response,
            Order = userMessage.Order.ToString(),
            ConversationId = userMessage.ConversationId.ToString(),
            CreatedAt = userMessage.CreatedAt.ToString(CultureInfo.InvariantCulture),
            UpdatedAt = userMessage.UpdatedAt.ToString()
        };
    }

    public async Task<List<ConversationResponse>> GetUserAllConversions(Guid userId)
    {
        var conversations = await _db.Conversations
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => new ConversationResponse
            {
                Title = c.Title,
                UserId = c.UserId.ToString(),
                IsActive = c.IsActive,
                MessageCount = c.MessageCount,
                Summary = c.Summary,
            }).ToListAsync();

        return conversations;
    }

    // Fetch active or specific conversation messages
    public async Task<List<ChatMessageResponse>?> GetUserAllConversationMessages(Guid userId, Guid? conversationId)
    {
        var conversation = conversationId == null ? 
            await _db.Conversations
                .AsNoTracking()
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.UserId == userId && c.IsActive) : 
            await _db.Conversations
                .AsNoTracking()
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);

        if (conversation == null)
        {
            return null;
        }
        
        var resp = conversation.Messages.Select(m => new ChatMessageResponse
        {
            Id = m.Id.ToString(),
            Role = m.Role.ToString(),
            ConversationId = m.ConversationId.ToString(),
            Order = m.Order.ToString(),
            Content = m.Content,
            CreatedAt = m.CreatedAt.ToString(CultureInfo.InvariantCulture),
            UpdatedAt = m.UpdatedAt.ToString(),
        }).ToList();
        
        return resp;
    }

    private static MessageRole MapToMessageRole(UserRole userRole)
    {
        return userRole switch
        {
            UserRole.User => MessageRole.User,
            UserRole.Admin => MessageRole.System,
            UserRole.Assistant => MessageRole.Assistant,
            _ => MessageRole.System
        };
    }
}