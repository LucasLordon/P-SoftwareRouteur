using Microsoft.AspNetCore.Authorization;

namespace SoftwareRouteur.Filters;

public class RequireChildProfileAttribute : AuthorizeAttribute
{
    public RequireChildProfileAttribute()
    {
        AuthenticationSchemes = "ProfileCookie";
        Roles = "child";
    }
}
