using System.Security.Claims;
using AIRagAPI.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using AIRagAPI.Services.DTOs;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

namespace AIRagAPI.Controllers;
[ApiController]
[Route("api/auth")]
public class AuthController (IAuthService authService, IConfiguration config, ILogger<AuthController> logger): ControllerBase
{
    private string FrontendUrl() => config["Frontend:BaseUrl"]!;
    
    /// <summary>
    /// Frontend: window.location.href = "/api/auth/google/login"
    /// Initiates Google OAuth flow. Middleware handles callback automatically.
    /// </summary>
    [HttpGet("google/login")]
    public async Task<IActionResult> GoogleLogin()
    {
        logger.LogInformation("Google OAuth login initiated from {Host}", Request.Host);
        
        var frontendUrl = FrontendUrl();
        
        // After OAuth succeeds, redirect to frontend
        await HttpContext.ChallengeAsync("Google", new AuthenticationProperties
        {
            RedirectUri = frontendUrl // Redirect to frontend after successful auth
        });
        
        return new EmptyResult();
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        var resp = new Response<string>()
        {
            Message = "Logout successfully.",
            Data = null,
            IsSuccess = true
        };
        return Ok(resp);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var errorResp = new Response<string>()
        {
            Message = "You session is expired, please login again.",
            Data = null,
            IsSuccess = false
        };
        
        if (!User.Identity?.IsAuthenticated ?? true)
            return Unauthorized(errorResp);
        
        var email = User.FindFirst(c => c.Type == ClaimTypes.Email)?.Value;
        if (email == null)
            return Unauthorized(errorResp);
        
        var user = await authService.FindUserAsync(email);
        if (user == null)
            return Unauthorized(errorResp);

        var successResp = new Response<UserResponse>()
        {
            Message = "You currently signed in.",
            Data = new UserResponse
            {
                Email = user.Email,
                Name = user.Name,
                PictureUrl = user.PictureUrl,
                Id = user.Id.ToString()
            },
            IsSuccess = true
        };
        return Ok(successResp);
    }
}