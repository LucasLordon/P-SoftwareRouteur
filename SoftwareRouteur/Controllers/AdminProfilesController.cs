using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SoftwareRouteur.Data;
using SoftwareRouteur.Models;
using SoftwareRouteur.ViewModels;

namespace SoftwareRouteur.Controllers;

[Authorize]
[Route("admin/profiles")]
public class AdminProfilesController : Controller
{
    private static readonly System.Text.RegularExpressions.Regex PinRegex =
        new(@"^\d{4}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private readonly AppDbContext _context;
    private readonly IStringLocalizer<AdminProfilesController> _localizer;

    public AdminProfilesController(AppDbContext context, IStringLocalizer<AdminProfilesController> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var profiles = _context.Profiles
            .Include(p => p.CreatedBy)
            .OrderBy(p => p.Role == "child")
            .ThenBy(p => p.DisplayName)
            .ToList();

        var clientCounts = _context.Clients
            .Where(c => c.ProfileId != null)
            .GroupBy(c => c.ProfileId)
            .ToDictionary(g => g.Key!.Value, g => g.Count());

        ViewBag.ClientCounts = clientCounts;
        return View(profiles);
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(ProfileCreateViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(vm.DisplayName) || string.IsNullOrWhiteSpace(vm.Role))
        {
            TempData["Error"] = _localizer["Error_FieldsRequired"].Value;
            return RedirectToAction("Index");
        }

        if (vm.Role == "parent" && string.IsNullOrWhiteSpace(vm.Pin))
        {
            TempData["Error"] = _localizer["Error_ParentPinRequired"].Value;
            return RedirectToAction("Index");
        }

        if (!string.IsNullOrWhiteSpace(vm.Pin))
        {
            if (!PinRegex.IsMatch(vm.Pin))
            {
                TempData["Error"] = _localizer["Error_PinFormat"].Value;
                return RedirectToAction("Index");
            }
            if (vm.Pin != vm.ConfirmPin)
            {
                TempData["Error"] = _localizer["Error_PinMismatch"].Value;
                return RedirectToAction("Index");
            }
        }

        var profile = new Profile
        {
            DisplayName = vm.DisplayName.Trim(),
            Role = vm.Role,
            PinHash = string.IsNullOrWhiteSpace(vm.Pin) ? null : BCrypt.Net.BCrypt.HashPassword(vm.Pin),
            CreatedById = null,
            CreatedAt = DateTime.Now
        };

        _context.Profiles.Add(profile);
        await _context.SaveChangesAsync();

        TempData["Success"] = string.Format(_localizer["Success_Created"].Value, profile.DisplayName);
        return RedirectToAction("Index");
    }

    [HttpPost("edit/{id}")]
    public async Task<IActionResult> Edit(int id, ProfileEditViewModel vm)
    {
        var profile = _context.Profiles.Find(id);
        if (profile == null)
            return RedirectToAction("Index");

        if (string.IsNullOrWhiteSpace(vm.DisplayName) || string.IsNullOrWhiteSpace(vm.Role))
        {
            TempData["Error"] = _localizer["Error_FieldsRequired"].Value;
            return RedirectToAction("Index");
        }

        if (!string.IsNullOrWhiteSpace(vm.Pin))
        {
            if (!PinRegex.IsMatch(vm.Pin))
            {
                TempData["Error"] = _localizer["Error_PinFormat"].Value;
                return RedirectToAction("Index");
            }
            if (vm.Pin != vm.ConfirmPin)
            {
                TempData["Error"] = _localizer["Error_PinMismatch"].Value;
                return RedirectToAction("Index");
            }
        }

        profile.DisplayName = vm.DisplayName.Trim();
        profile.Role = vm.Role;

        if (!string.IsNullOrWhiteSpace(vm.Pin))
            profile.PinHash = BCrypt.Net.BCrypt.HashPassword(vm.Pin);

        await _context.SaveChangesAsync();

        TempData["Success"] = string.Format(_localizer["Success_Updated"].Value, profile.DisplayName);
        return RedirectToAction("Index");
    }

    [HttpPost("delete/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var profile = _context.Profiles.Find(id);
        if (profile == null)
            return RedirectToAction("Index");

        var assignedClients = _context.Clients.Where(c => c.ProfileId == id).ToList();
        foreach (var client in assignedClients)
            client.ProfileId = null;

        _context.Profiles.Remove(profile);
        await _context.SaveChangesAsync();

        TempData["Success"] = string.Format(_localizer["Success_Deleted"].Value, profile.DisplayName);
        return RedirectToAction("Index");
    }
}
