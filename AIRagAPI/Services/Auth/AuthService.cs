using System.Security.Claims;
using AIRagAPI.Domain.Entities;
using AIRagAPI.Domain.Enums;
using AIRagAPI.Domain.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace AIRagAPI.Services.Auth;

public class AuthService(ILogger<AuthService> logger, AppDbContext db, IHttpContextAccessor httpContextAccessor): IAuthService
{
    private HttpContext HttpContext => httpContextAccessor.HttpContext!;
    
    /// <summary>
    /// Will validate user - Create User if it doesn't exist and returns claims principle for google oauth
    /// </summary>
    /// <param name="email"></param>
    /// <param name="name"></param>
    /// <param name="picture"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="Exception"></exception>
    public async Task<ClaimsPrincipal> ValidateUserAsync(string email, string? name, string? picture, CancellationToken cancellationToken)
    {
        try
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
            if (user == null)
            {
                var role = email.Equals("samuelokundalaiye@gmail.com", StringComparison.CurrentCultureIgnoreCase) ? UserRole.Admin : UserRole.User;
                user = new User
                {
                    Email = email,
                    Name = name ?? "",
                    PictureUrl = picture,
                    Role = role
                };
                
                await db.Users.AddAsync(user,  cancellationToken);
            } else if (user.PictureUrl != picture)
            {
                // Update picture if change
                user.PictureUrl = picture;
            }
            await db.SaveChangesAsync(cancellationToken);
            
            // Sign in User (create cookie session)
            var identity = await CreateCookie(user);
            return identity;
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

    private async Task<ClaimsPrincipal> CreateCookie(User user)
    {
        logger.LogInformation("Creating cookie for user {Email}", user.Email);
        logger.LogInformation("Creating cookie for user {Id}", user.Id);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim("UserId", user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
        };
            
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        // logger.LogInformation("User after sign-in: {Id}",
        //     HttpContext.User.FindFirstValue("UserId"));
        return principal;
    }
}