using Microsoft.AspNetCore.Mvc;
using SoftwareRouteur.Data;
using SoftwareRouteur.Filters;
using SoftwareRouteur.ViewModels;
using System.Security.Claims;

namespace SoftwareRouteur.Controllers;

[RequireChildProfile]
[Route("child")]
public class ChildController : Controller
{
    private readonly AppDbContext _context;

    public ChildController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("home")]
    public IActionResult Home()
    {
        var profileId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var currentProfile = _context.Profiles.Find(profileId)!;

        var clients = _context.Clients
            .Where(c => c.ProfileId == profileId)
            .OrderBy(c => c.Hostname)
            .ToList();

        var vm = new ChildHomeViewModel
        {
            CurrentProfile = currentProfile,
            AssignedClients = clients
        };

        return View(vm);
    }

    [HttpGet("challenges")]
    public IActionResult Challenges() => View();

    [HttpGet("devices")]
    public IActionResult Devices() => View();
}
