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
    /// <param name="question"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> Ask([FromQuery] string question)
    {
        try
        {
            var data = await _vectorService.AskAsync(question);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while asking for question");
            return StatusCode(500, "An error occured while asking for question");
        }
    }
}