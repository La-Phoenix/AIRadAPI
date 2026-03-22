using AIRagAPI.Services.Vector;
using Microsoft.AspNetCore.Mvc;

namespace AIRagAPI.Controllers;

[ApiController]
[Route("api/vector")]
public class VectorController: ControllerBase
{
    private readonly IVectorService _vectorService;
    private readonly ILogger<VectorController> _logger;
    
    public VectorController(IVectorService vectorService, ILogger<VectorController> logger)
    {
        _vectorService = vectorService;
        _logger = logger;
    }

    /// <summary>
    /// Create Text Vector for RAG functionality
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    [HttpPost("add")]
    public async Task<IActionResult> Add([FromBody] string text)
    {
        try
        {
            await _vectorService.AddDocumentAsync(text);
            return Ok("The document added");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding text vector");
            return StatusCode(500, "Error Creating text vector");
        }
    }

    
    /// <summary>
    /// Search Text for RAG functionality
    /// </summary>
    /// <param name="query"></param>
    /// <param name="page"></param>
    /// <param name="top"></param>
    /// <returns></returns>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int page, [FromQuery] ulong top = 3)
    {
        try
        {
            var result = await _vectorService.SearchAsync(query, top);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Searching Text Vector");
            return StatusCode(500, "Error Searching Text Vector");
        }
    }
}