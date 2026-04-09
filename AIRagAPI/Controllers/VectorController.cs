using System.ComponentModel.DataAnnotations;
using AIRagAPI.Common.UserContext;
using AIRagAPI.Services.Vector;
using AIRagAPI.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIRagAPI.Controllers;

[ApiController]
[Route("api/vector")]
[Authorize]
public class VectorController(
    IVectorService vectorService,
    IUserContextService userContextService,
    ILogger<VectorController> logger)
    : ControllerBase
{
    /// <summary>
    /// Create Text Vector for RAG functionality
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("add")]
    public async Task<IActionResult> Add([FromBody] AddDocRequest request)
    {
        var badRequest = new Response<string>()
        {
            Message = "",
            Data = null,
            IsSuccess = false
        };
        try
        {
            var userId = userContextService.GetUserId();
            if (userId == null)
            {
                badRequest.Message = "User not found or invalid user id";
                return Unauthorized(badRequest);
            }
            
            await vectorService.AddDocumentAsync(request.Text, userId.Value);
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
            logger.LogError(ex, "Error adding text vector");
            badRequest.Message = "Error adding text vector";
            return StatusCode(500, badRequest);
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
        var badRequest = new Response<string>()
        {
            Message = "",
            Data = null,
            IsSuccess = false
        };
        
        try
        {
            var userId = userContextService.GetUserId();
            if (userId == null)
            {
                badRequest.Message = "User not found or invalid user id";
                return Unauthorized(badRequest);
            }
            var result = await vectorService.SearchAsync(query, userId.Value, top);
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
            logger.LogError(ex, "Error Searching Text Vector");
            badRequest.Message = "Error Searching Text Vector";
            return StatusCode(500, badRequest);
        }
    }
    
}