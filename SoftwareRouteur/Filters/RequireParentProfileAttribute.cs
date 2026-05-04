using Microsoft.AspNetCore.Authorization;

namespace SoftwareRouteur.Filters;

public class RequireParentProfileAttribute : AuthorizeAttribute
{
    public RequireParentProfileAttribute()
    {
        AuthenticationSchemes = "ProfileCookie";
        Roles = "parent";
    }
}
