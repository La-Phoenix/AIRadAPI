using AIRagAPI.Agents;
using AIRagAPI.Services.Agents;
using AIRagAPI.Services.Vector;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;

namespace AIRagAPI.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController: ControllerBase
{
    private readonly Kernel _kernel;
    private readonly ILogger<ChatController> _logger;
    private readonly IVectorService _vectorService;
    public ChatController(Kernel kernel, IVectorService vectorService, ILogger<ChatController> logger)
    {
        _kernel = kernel;
        _vectorService = vectorService;
        _logger = kernel.LoggerFactory.CreateLogger<ChatController>();
    }

    /// <summary>
    /// What's on Your Mind? Ask what you'd like to know about
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] ChatRequest request)
    {
        try
        {
            var agents = new List<IAgent>
            {
                new RetrieverAgent(_vectorService),
                new SummarizeAgent(_kernel)
            };
            
            var coordinator = new AgentCoordinator(agents);
            
            var answer = await coordinator.AskAsync(request.Question);

            var chatResponse = new ChatResponse
            {
                Question = request.Question,
                Message = answer
            };
            return Ok(chatResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while asking for question");
            return StatusCode(500, "An error occured while asking for question");
        }
    }

    public record ChatRequest
    {
        public required string Question { get; init; }
    }

    public record ChatResponse
    {
        public required string Question { get; init; }
        public required string Message { get; init; }
    }
}