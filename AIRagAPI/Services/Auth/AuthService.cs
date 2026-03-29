using System.Security.Claims;
using AIRagAPI.Domain.Entities;
using AIRagAPI.Domain.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace AIRagAPI.Services.Auth;

public class AuthService(ILogger<AuthService> logger, AppDbContext db, IHttpContextAccessor httpContextAccessor): IAuthService
{
    private HttpContext HttpContext => httpContextAccessor.HttpContext!;
    
    /// <summary>
    /// Will validate user - Create User if it doesn't exist and sign in user
    /// </summary>
    /// <param name="email"></param>
    /// <param name="name"></param>
    /// <param name="picture"></param>
    /// <exception cref="Exception"></exception>
    public async Task ValidateUserAsync(string email, string? name, string? picture)
    {
        try
        {
            var user = db.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                user = new User
                {
                    Email = email,
                    Name = name ?? "",
                    PictureUrl = picture,
                };
                
                await db.Users.AddAsync(user);
                await db.SaveChangesAsync();
            }
            
            // Sign in User (create cookie session)
            await CreateCookie(user);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occured while validating user");
            throw new Exception("An error occured while validating user");
        }
    }

    public async Task<User?> FindUserAsync(string email)
    {
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);
        return user;
    }

    private async Task CreateCookie(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
        };
            
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        
        await HttpContext.SignInAsync("Cookies", principal);
    }
}