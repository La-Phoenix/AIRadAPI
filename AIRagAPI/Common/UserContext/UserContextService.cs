using System.Security.Claims;

namespace AIRagAPI.Common.UserContext;

public class UserContextService(IHttpContextAccessor contextAccessor): IUserContextService
{
    public Guid? GetUserId()
    {
        var user = contextAccessor?.HttpContext?.User;
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return null;
        }

        if (!Guid.TryParse(userId, out var userGuid))
        {
            return null;
        }
        return userGuid;
    }
}