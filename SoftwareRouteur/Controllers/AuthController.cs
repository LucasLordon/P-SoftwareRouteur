using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using SoftwareRouteur.Data;
using SoftwareRouteur.ViewModels;
using System.Security.Claims;

namespace SoftwareRouteur.Controllers;

public class AuthController : Controller
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _context;
    private readonly IStringLocalizer<AuthController> _localizer;
    private readonly IMemoryCache _cache;

    public AuthController(AppDbContext context, IStringLocalizer<AuthController> localizer, IMemoryCache cache)
    {
        _context = context;
        _localizer = localizer;
        _cache = cache;
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
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var cacheKey = $"login_attempts_{ip}";

        if (_cache.TryGetValue(cacheKey, out int attempts) && attempts >= MaxFailedAttempts)
            return View(new LoginViewModel { Error = _localizer["Error_TooManyAttempts"].Value });

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return View(new LoginViewModel { Error = _localizer["Error_FieldsRequired"].Value });

        var user = _context.AdminUsers.FirstOrDefault(u => u.Username == username);

        if (user == null)
        {
            RecordFailedAttempt(cacheKey);
            return View(new LoginViewModel { Error = _localizer["Error_InvalidCredentials"].Value });
        }

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
        {
            RecordFailedAttempt(cacheKey);
            return View(new LoginViewModel { Error = _localizer["Error_InvalidCredentials"].Value });
        }

        _cache.Remove(cacheKey);

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

    private void RecordFailedAttempt(string cacheKey)
    {
        var attempts = _cache.TryGetValue(cacheKey, out int current) ? current : 0;
        _cache.Set(cacheKey, attempts + 1, LockoutDuration);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignOutAsync("ProfileCookie");
        return RedirectToAction("Index", "Profiles");
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = "ProfileCookie")]
    public async Task<IActionResult> ProfileLogout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignOutAsync("ProfileCookie");
        return RedirectToAction("Index", "Profiles");
    }
}
