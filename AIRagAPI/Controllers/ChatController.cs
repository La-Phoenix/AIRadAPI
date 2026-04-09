
using System.Security.Claims;
using AIRagAPI.Agents;
using AIRagAPI.Common.UserContext;
using AIRagAPI.Services.Agents;
using AIRagAPI.Services.ChatService;
using AIRagAPI.Services.DTOs;
using AIRagAPI.Services.Vector;
using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;

namespace AIRagAPI.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController: ControllerBase
{
    private readonly Kernel _kernel;
    private readonly ILogger<ChatController> _logger;
    private readonly IVectorService _vectorService;
    private readonly IChatService _chatService;
    private readonly IUserContextService _userContextService;
    public ChatController(Kernel kernel, IVectorService vectorService, IChatService chatService, IUserContextService userContextService, ILogger<ChatController> logger)
    {
        _kernel = kernel;
        _vectorService = vectorService;
        _chatService = chatService;
        _userContextService = userContextService;
        _logger = kernel.LoggerFactory.CreateLogger<ChatController>();
    }

    /// <summary>
    /// What's on Your Mind? Ask what you'd like to know about
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var badResp = new Response<string>
        {
            Message = "",
            Data = null,
            IsSuccess = false
        };
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if(!Guid.TryParse(userId, out var userGuid))
            {
                badResp.Message = "Invalid user id";
                return Unauthorized(badResp);
            }
            var chatResponse = await _chatService.SendMessage(userGuid, request, cancellationToken);
           
            var resp = new Response<ChatMessageResponse>
            {
                Message = "Chat request successful.",
                Data = chatResponse,
                IsSuccess = true
            };
            return Ok(resp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while asking for question");
            badResp.Message = "An error occured while asking for question";
            return StatusCode(500, badResp);
        }
    }

    [HttpGet("conversations/user")]
    public async Task<IActionResult> GetAllUserConversations(CancellationToken cancellationToken)
    {
        var badReq = new Response<string>()
        {
            Message = "",
            Data = null,
            IsSuccess = false
        };
        try
        {
            var userId = User.FindFirstValue( ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                badReq.Message = "User not found";
                return Unauthorized(badReq);
            }

            if (!Guid.TryParse(userId, out var userGuid))
            {
                badReq.Message = "Invalid user id";
                return Unauthorized(badReq);
            }
            var conversations = await _chatService.GetUserAllConversions(userGuid, cancellationToken);
            var resp = new Response<List<ConversationResponse>>
            {
                Message = "Fetched all user conversations successful.",
                Data = conversations,
                IsSuccess = true
            };
            return Ok(resp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something went wrong while fetching conversations");
            badReq.Message = "Something went wrong while fetching conversations";
            return StatusCode(500, badReq);
        }
    }

    // [HttpPost("conversations")]
    // public async Task<IActionResult> CreateConversation(CancellationToken cancellationToken)
    // {
    //     var badReq = new Response<string>()
    //     {
    //         Message = "",
    //         Data = null,
    //         IsSuccess = false
    //     };
    //     try
    //     {
    //         var userId = GetUserId();
    //         if (userId == null)
    //         {
    //             badReq.Message = "User not found or id invalid";
    //             return Unauthorized(badReq);
    //         }
    //         
    //         
    //     } catch(Exception ex)
    //     {
    //         _logger.LogError(ex, "Something went wrong while creating conversation");
    //         badReq.Message = "Something went wrong while creating conversation";
    //         return StatusCode(500, badReq);
    //     }
    // }
    [HttpGet("conversations/messages")]
    public async Task<IActionResult> GetUserAllConversationMessages([FromQuery] string? conversationId, CancellationToken cancellationToken)
    {
        var badReq = new Response<string>()
        {
            Message = "",
            Data = null,
            IsSuccess = false
        };
        try
        {
            var userId = _userContextService.GetUserId();
            if (userId is null)
            {
                badReq.Message = "User not found or id invalid";
                return Unauthorized(badReq);
            }

            Guid? conversationGuid = null;
            if (!string.IsNullOrWhiteSpace(conversationId))
            {
                if (!Guid.TryParse(conversationId, out var parsedConversationGuid))
                {
                    badReq.Message = "Invalid conversation id";
                    return Unauthorized(badReq);
                }
                conversationGuid = parsedConversationGuid;
            }
            var conversationMessages = await _chatService.GetUserAllConversationMessages(userId.Value, conversationGuid, cancellationToken);

            if (conversationMessages == null)
            {
                badReq.Message = "Conversation not found";
                return NotFound(badReq);
            }

            var rep = new Response<List<ChatMessageResponse>>
            {
                Message = "Fetched all conversation messages successful.",
                Data = conversationMessages,
                IsSuccess = true
            };
            return Ok(rep);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something went wrong while trying to fetch this conversation messages");
            badReq.Message = "Something went wrong while trying to fetch this conversation messages";
            return StatusCode(500, badReq);
        }
    }

    
}