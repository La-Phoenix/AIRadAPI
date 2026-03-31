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
    
    [HttpGet("google")]
    public async Task LoginWithGoogle()
    {
        var redirectUri = Url.Action(
            action: "GoogleCallback",
            controller: "Auth",
            values: null,
            protocol: Request.Scheme,   // picks up https via forwarded headers
            host: Request.Host.Value
        );

        await HttpContext.ChallengeAsync(GoogleDefaults.AuthenticationScheme,
            new AuthenticationProperties
            {
                RedirectUri = redirectUri
            });
    }
    
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback(CancellationToken cancellationToken)
    {
        try
        {
            // Read external cookie on redirect
            // var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            // if (!result.Succeeded)
            //     return Unauthorized();

            var unAuthResp = new Response<string>()
            {
                Message = "Google Auth Failed",
                Data = null,
                IsSuccess = false
            };
            
            // Using app cookie since middleware already signed in user -> i.e DefaultSignInScheme
            if ( User.Identity is not { IsAuthenticated: true })
            {
                return Redirect($"{FrontendUrl()}/login?error=auth_failed");
            }
        
            var email = User.FindFirstValue(ClaimTypes.Email);
            var name = User.FindFirstValue(ClaimTypes.Name);
            var picture = User.FindFirstValue("picture");

            if (string.IsNullOrEmpty(email))
            {
                var resp = new Response<string>
                {
                    Message = "Email not found.",
                    Data = null,
                    IsSuccess = false
                };
                return Redirect($"{FrontendUrl()}/login?error=auth_failed");
            }
        
            // Validate user and create app cookie
            await authService.ValidateUserAsync(email, name, picture,  cancellationToken); 
            
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
            return Redirect($"{FrontendUrl()}/login?error=server_error");
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