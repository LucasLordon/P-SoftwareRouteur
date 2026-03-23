using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace SoftwareRouteur.Controllers;

public class CultureController : Controller
{
    [HttpPost]
    public IActionResult Set(string culture, string returnUrl)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true }
        );
        return LocalRedirect(returnUrl);
    }
}
