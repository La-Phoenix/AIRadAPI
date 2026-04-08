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

    public async Task<ChatMessageResponse> SendMessage(Guid userId, ChatRequest request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FindAsync([userId], cancellationToken);
        if (user == null) throw new Exception("User not found");
        
        Guid? conversationId = null;
        if (string.IsNullOrEmpty(request.ConversationId))
        {
            if (!Guid.TryParse(request.ConversationId, out var convId))
            {
                throw new Exception("Invalid conversationId");
            }
            conversationId = convId;
        }

        Conversation? conversation = null;
        if (conversationId == null)
        {
            // Get active conversation
            conversation = await _db.Conversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.UserId == userId && c.IsActive, cancellationToken: cancellationToken);
        }
        else
        {
            conversation =  await _db.Conversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Id == conversationId, cancellationToken: cancellationToken);
        }
        
        // Create a new one if none exist. If above message limit disable conversion
        if (request.IsNewConversation || conversation == null || conversation.MessageCount >= _settings.MaxMessages)
        {
            if (conversation != null || (request.IsNewConversation && conversation != null)) conversation.IsActive = false;
            var title = GenerateConversationTitle(request.Question);
            conversation = new Conversation
            {
                Title = string.IsNullOrEmpty(title) ? "New Chat" : title,
                UserId = userId,
                IsActive = true,
            };
            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Message order
        int order = conversation.MessageCount + 1;
        
        // User message - first msg
        var userMessage = new Message
        {
            ConversationId = conversation.Id,
            Order = order,
            Content = request.Question,
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
        {request.Question}
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
        
        await _db.SaveChangesAsync(cancellationToken);
        
        return new ChatMessageResponse
        {
            Id = aiMessage.Id.ToString(),
            Role = aiMessage.Role.ToString(),
            Content = aiMessage.Content,
            Order = aiMessage.Order.ToString(),
            ConversationId = aiMessage.ConversationId.ToString(),
            CreatedAt = aiMessage.CreatedAt.ToString(CultureInfo.InvariantCulture),
            UpdatedAt = aiMessage.UpdatedAt.ToString()
        };
    }

    public async Task<List<ConversationResponse>> GetUserAllConversions(Guid userId, CancellationToken cancellationToken)
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
            }).ToListAsync(cancellationToken: cancellationToken);

        return conversations;
    }

    // Fetch active or specific conversation messages
    public async Task<List<ChatMessageResponse>?> GetUserAllConversationMessages(Guid userId, Guid? conversationId, CancellationToken cancellationToken)
    {
        var conversation = conversationId == null ? 
            await _db.Conversations
                .AsNoTracking()
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.UserId == userId && c.IsActive, cancellationToken) : 
            await _db.Conversations
                .AsNoTracking()
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId, cancellationToken);

        var resp = conversation?.Messages.Select(m => new ChatMessageResponse
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

    // public Task<ConversationResponse> CreateConversation(string message, Guid userId, CancellationToken cancellationToken)
    // {
    //     var activeConversation = _db.Conversations.FirstOrDefault(c => c.IsActive);
    //     
    //         
    //     var title = GenerateConversationTitle(message);
    //
    //     var conversation = new Conversation
    //     {
    //         Title = title,
    //         MessageCount = 0,
    //         UserId = userId,
    //         IsActive = true,
    //     };
    //     if (activeConversation != null) conversation.IsActive = false;
    //     _db.Conversations.Add(conversation);
    // }

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

    private static string GenerateConversationTitle(string message)
    {
        return string.Join(" ", message.Split(" ", StringSplitOptions.RemoveEmptyEntries).Take(6));
    }
}