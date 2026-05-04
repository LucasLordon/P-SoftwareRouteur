using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using SoftwareRouteur.Data;
using SoftwareRouteur.Models;
using SoftwareRouteur.ViewModels;
using System.Security.Claims;

namespace SoftwareRouteur.Controllers;

public class ProfilesController : Controller
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const string ProfileCookieScheme = "ProfileCookie";

    private readonly AppDbContext _context;
    private readonly IStringLocalizer<ProfilesController> _localizer;
    private readonly IMemoryCache _cache;

    public ProfilesController(
        AppDbContext context,
        IStringLocalizer<ProfilesController> localizer,
        IMemoryCache cache)
    {
        _context = context;
        _localizer = localizer;
        _cache = cache;
    }

    /// <summary>
    /// GET /profiles — profile selection page (public).
    /// Authenticated admins are redirected immediately to the dashboard.
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        var profiles = _context.Profiles.ToList();
        return View(profiles);
    }

    /// <summary>
    /// GET /profiles/pin/{id} — show PIN entry for the given profile,
    /// or redirect straight to the dashboard when no PIN is set.
    /// </summary>
    [HttpGet]
    public IActionResult Pin(int id)
    {
        var profile = _context.Profiles.FirstOrDefault(p => p.Id == id);
        if (profile == null)
            return RedirectToAction("Index");

        if (profile.PinHash == null)
            return RedirectBasedOnRole(profile);

        return View(new PinViewModel { Profile = profile });
    }

    /// <summary>
    /// POST /profiles/pin/{id} — verify the PIN, sign in with ProfileCookie,
    /// and redirect to the appropriate family dashboard.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Pin(int id, string pin)
    {
        var profile = _context.Profiles.FirstOrDefault(p => p.Id == id);
        if (profile == null)
            return RedirectToAction("Index");

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var cacheKey = $"pin_attempts_{ip}_{id}";

        if (_cache.TryGetValue(cacheKey, out int attempts) && attempts >= MaxFailedAttempts)
            return View(new PinViewModel { Profile = profile, Error = _localizer["Error_TooManyAttempts"].Value });

        bool valid = false;
        try
        {
            valid = BCrypt.Net.BCrypt.Verify(pin, profile.PinHash);
        }
        catch
        {
            return View(new PinViewModel { Profile = profile, Error = _localizer["Error_InvalidPin"].Value });
        }

        if (!valid)
        {
            RecordFailedAttempt(cacheKey);
            return View(new PinViewModel { Profile = profile, Error = _localizer["Error_InvalidPin"].Value });
        }

        _cache.Remove(cacheKey);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, profile.Id.ToString()),
            new Claim(ClaimTypes.Name, profile.DisplayName),
            new Claim(ClaimTypes.Role, profile.Role)
        };

        var identity = new ClaimsIdentity(claims, ProfileCookieScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            ProfileCookieScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            }
        );

        return RedirectBasedOnRole(profile);
    }

    private IActionResult RedirectBasedOnRole(Profile profile)
    {
        return profile.Role == "parent"
            ? RedirectToAction("Dashboard", "Parent")
            : RedirectToAction("Home", "Child");
    }

    private void RecordFailedAttempt(string cacheKey)
    {
        var attempts = _cache.TryGetValue(cacheKey, out int current) ? current : 0;
        _cache.Set(cacheKey, attempts + 1, LockoutDuration);
    }
}
