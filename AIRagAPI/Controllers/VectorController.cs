using System.ComponentModel.DataAnnotations;
using AIRagAPI.Services.Vector;
using AIRagAPI.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIRagAPI.Controllers;

[ApiController]
[Route("api/vector")]
[Authorize]
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
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("add")]
    public async Task<IActionResult> Add([FromBody] AddDocRequest request)
    {
        try
        {
            await _vectorService.AddDocumentAsync(request.Text);
            var resp = new Response<string>
            {
                Message = "Document added successfully.",
                Data = null,
                IsSuccess = true
            };
            return StatusCode(201, resp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding text vector");
            var response = new Response<string>
            {
                Message = "Error Creating text vector",
                Data = null,
                IsSuccess = false
            };
            return StatusCode(500, response);
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
    public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] ulong top = 3)
    {
        try
        {
            var result = await _vectorService.SearchAsync(query, top);
            var resp = new Response<List<string>>
            {
                Message = "Search successful.",
                Data = result,
                IsSuccess = true
            };
            return Ok(resp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Searching Text Vector");
            var resp = new Response<string>
            {
                Message = "Error Searching Text Vector",
                Data = null,
                IsSuccess = false
            };
            return StatusCode(500, "Error Searching Text Vector");
        }
    }
    
}