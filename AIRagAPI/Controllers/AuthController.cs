using System.Security.Claims;
using AIRagAPI.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using AIRagAPI.Services.DTOs;

namespace AIRagAPI.Controllers;
[ApiController]
[Route("api/auth")]
public class AuthController (IAuthService authService, IConfiguration config, ILogger<AuthController> logger): ControllerBase
{
    private string FrontendUrl() => config["Frontend:BaseUrl"]!;
    
    [HttpGet("google")]
    public IActionResult LoginWithGoogle()
    {
        var redirectUrl = Url.Action("GoogleCallback");
        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl
        };
        return Challenge(properties, "Google");
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback()
    {
        try
        {
            var result = await HttpContext.AuthenticateAsync("Cookies");
            if (!result.Succeeded)
                return Unauthorized();
        
            var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
            var name = result.Principal.FindFirst(ClaimTypes.Name)?.Value;
            var picture = result.Principal.FindFirst("picture")?.Value;

            if (email == null)
            {
                var resp = new Response<string>
                {
                    Message = "Email not found.",
                    Data = null,
                    IsSuccess = false
                };
                return BadRequest(resp);
            }
        
            await authService.ValidateUserAsync(email, name, picture);
            return Redirect(FrontendUrl());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during authentication.");
            var resp = new Response<string>()
            {
                Message = "An error occurred while authenticating your account.",
                Data = null,
                IsSuccess = false
            };
            return StatusCode(500, resp);
        }
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