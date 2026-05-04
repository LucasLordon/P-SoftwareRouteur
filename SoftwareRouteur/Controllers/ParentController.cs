using Microsoft.AspNetCore.Mvc;
using SoftwareRouteur.Data;
using SoftwareRouteur.Filters;
using SoftwareRouteur.ViewModels;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace SoftwareRouteur.Controllers;

[RequireParentProfile]
[Route("parent")]
public class ParentController : Controller
{
    private static readonly Regex PinRegex = new(@"^\d{4}$", RegexOptions.Compiled);

    private readonly AppDbContext _context;

    public ParentController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("dashboard")]
    public IActionResult Dashboard()
    {
        var profileId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var currentProfile = _context.Profiles.Find(profileId)!;

        var children = _context.Profiles
            .Where(p => p.CreatedById == profileId)
            .OrderBy(p => p.DisplayName)
            .ToList();

        var childIds = children.Select(c => c.Id).ToList();
        var deviceCounts = _context.Clients
            .Where(c => c.ProfileId != null && childIds.Contains(c.ProfileId.Value))
            .GroupBy(c => c.ProfileId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var vm = new ParentDashboardViewModel
        {
            CurrentProfile = currentProfile,
            ChildProfiles = children.Select(c => new ChildSummary
            {
                Profile = c,
                DeviceCount = deviceCounts.GetValueOrDefault(c.Id, 0)
            }).ToList()
        };

        return View(vm);
    }

    [HttpGet("profils")]
    public IActionResult Profils() => View();

    [HttpGet("challenges")]
    public IActionResult Challenges() => View();

    [HttpGet("regles")]
    public IActionResult Regles() => View();

    [HttpGet("security")]
    public IActionResult Security() => View(new ParentSecurityViewModel());

    [HttpPost("security")]
    public async Task<IActionResult> Security(string currentPin, string newPin, string confirmPin)
    {
        var profileId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var profile = _context.Profiles.Find(profileId)!;

        if (!PinRegex.IsMatch(currentPin ?? "") || !PinRegex.IsMatch(newPin ?? "") || !PinRegex.IsMatch(confirmPin ?? ""))
            return View(new ParentSecurityViewModel { ErrorMessage = "Security_Error_InvalidFormat" });

        if (profile.PinHash != null && !BCrypt.Net.BCrypt.Verify(currentPin, profile.PinHash))
            return View(new ParentSecurityViewModel { ErrorMessage = "Security_Error_WrongCurrent" });

        if (newPin != confirmPin)
            return View(new ParentSecurityViewModel { ErrorMessage = "Security_Error_Mismatch" });

        if (profile.PinHash != null && BCrypt.Net.BCrypt.Verify(newPin, profile.PinHash))
            return View(new ParentSecurityViewModel { ErrorMessage = "Security_Error_SamePin" });

        profile.PinHash = BCrypt.Net.BCrypt.HashPassword(newPin);
        await _context.SaveChangesAsync();

        return View(new ParentSecurityViewModel { SuccessMessage = "Security_Success" });
    }
}
