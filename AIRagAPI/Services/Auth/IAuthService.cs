using AIRagAPI.Domain.Entities;
using Microsoft.AspNetCore.Authentication;

namespace AIRagAPI.Services.Auth;

public interface IAuthService
{
    /// <summary>
    /// Will validate user - Create User if it doesn't exist and sign in user
    /// </summary>
    /// <param name="email"></param>
    /// <param name="name"></param>
    /// <param name="picture"></param>
    /// <exception cref="Exception"></exception>
    public Task ValidateUserAsync(string email, string? name, string? picture);
    
    public Task<User?> FindUserAsync(string email);
    
}