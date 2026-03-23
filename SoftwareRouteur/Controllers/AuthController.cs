using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Localization;
using SoftwareRouteur.Data;
using SoftwareRouteur.ViewModels;
using System.Security.Claims;

namespace SoftwareRouteur.Controllers;

public class AuthController : Controller
{
    private readonly AppDbContext _context;
    private readonly IStringLocalizer<AuthController> _localizer;

    public AuthController(AppDbContext context, IStringLocalizer<AuthController> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new LoginViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Login(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return View(new LoginViewModel { Error = _localizer["Error_FieldsRequired"].Value });

        var user = _context.AdminUsers
            .FirstOrDefault(u => u.Username == username);

        if (user == null)
            return View(new LoginViewModel { Error = _localizer["Error_InvalidCredentials"].Value });

        bool valid = false;
        try
        {
            valid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        }
        catch
        {
            return View(new LoginViewModel { Error = _localizer["Error_PasswordVerification"].Value });
        }

        if (!valid)
            return View(new LoginViewModel { Error = _localizer["Error_InvalidCredentials"].Value });

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            }
        );

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
}
