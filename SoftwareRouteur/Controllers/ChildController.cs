using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SoftwareRouteur.Data;
using SoftwareRouteur.Filters;
using SoftwareRouteur.Models;
using SoftwareRouteur.Services;
using SoftwareRouteur.ViewModels;
using System.Security.Claims;

namespace SoftwareRouteur.Controllers;

[RequireChildProfile]
[Route("child")]
public class ChildController : Controller
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private const long MaxFileSize = 5 * 1024 * 1024;

    private readonly AppDbContext _context;
    private readonly IStringLocalizer<ChildController> _localizer;
    private readonly OPNsenseService _opnsense;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ChildController> _logger;

    public ChildController(AppDbContext context, IStringLocalizer<ChildController> localizer, OPNsenseService opnsense, IWebHostEnvironment env, ILogger<ChildController> logger)
    {
        _context = context;
        _localizer = localizer;
        _opnsense = opnsense;
        _env = env;
        _logger = logger;
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

        var activeReward = _context.Rewards
            .Include(r => r.Challenge)
            .FirstOrDefault(r => r.ChildProfileId == profileId && r.Status == "active");

        var pausedReward = _context.Rewards
            .Include(r => r.Challenge)
            .FirstOrDefault(r => r.ChildProfileId == profileId && r.Status == "paused");

        var availableRewards = _context.Rewards
            .Include(r => r.Challenge)
            .Where(r => r.ChildProfileId == profileId && r.Status == "idle")
            .ToList();

        var vm = new ChildHomeViewModel
        {
            CurrentProfile = currentProfile,
            AssignedClients = clients,
            ActiveReward = activeReward ?? pausedReward,
            AvailableRewards = availableRewards
        };

        return View(vm);
    }

    [HttpGet("home/timer")]
    public IActionResult Timer()
    {
        var profileId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var reward = _context.Rewards
            .FirstOrDefault(r => r.ChildProfileId == profileId && (r.Status == "active" || r.Status == "paused"));

        if (reward == null)
            return Json(new { active = false });

        return Json(new
        {
            active = true,
            status = reward.Status,
            remainingSeconds = reward.RemainingSeconds,
            rewardId = reward.Id
        });
    }

    [HttpPost("home/activate/{rewardId:int}")]
    public async Task<IActionResult> ActivateReward(int rewardId)
    {
        var profileId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var reward = await _context.Rewards
            .Include(r => r.Challenge)
            .FirstOrDefaultAsync(r => r.Id == rewardId && r.ChildProfileId == profileId);

        if (reward == null)
        {
            TempData["Error"] = _localizer["Reward_Error_NotFound"].Value;
            return RedirectToAction("Home");
        }

        if (reward.Status != "idle")
        {
            TempData["Error"] = _localizer["Reward_Error_InvalidStatus"].Value;
            return RedirectToAction("Home");
        }

        reward.Status = "active";
        reward.ActivatedAt = DateTime.Now;
        reward.LastUpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();

        await RewardOPNsenseHelper.SetProfileDevicesBlockedAsync(
            _context, _opnsense, _logger, reward, blocked: false);

        TempData["Success"] = _localizer["Reward_Success_Activated"].Value;
        return RedirectToAction("Home");
    }

    [HttpPost("home/pause/{rewardId:int}")]
    public async Task<IActionResult> PauseReward(int rewardId)
    {
        var profileId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var reward = await _context.Rewards
            .Include(r => r.Challenge)
            .FirstOrDefaultAsync(r => r.Id == rewardId && r.ChildProfileId == profileId);

        if (reward == null)
        {
            TempData["Error"] = _localizer["Reward_Error_NotFound"].Value;
            return RedirectToAction("Home");
        }

        if (reward.Status != "active")
        {
            TempData["Error"] = _localizer["Reward_Error_InvalidStatus"].Value;
            return RedirectToAction("Home");
        }

        reward.Status = "paused";
        await _context.SaveChangesAsync();

        await RewardOPNsenseHelper.SetProfileDevicesBlockedAsync(
            _context, _opnsense, _logger, reward, blocked: true);

        TempData["Success"] = _localizer["Reward_Success_Paused"].Value;
        return RedirectToAction("Home");
    }

    [HttpPost("home/resume/{rewardId:int}")]
    public async Task<IActionResult> ResumeReward(int rewardId)
    {
        var profileId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var reward = await _context.Rewards
            .Include(r => r.Challenge)
            .FirstOrDefaultAsync(r => r.Id == rewardId && r.ChildProfileId == profileId);

        if (reward == null)
        {
            TempData["Error"] = _localizer["Reward_Error_NotFound"].Value;
            return RedirectToAction("Home");
        }

        if (reward.Status != "paused")
        {
            TempData["Error"] = _localizer["Reward_Error_InvalidStatus"].Value;
            return RedirectToAction("Home");
        }

        reward.Status = "active";
        reward.LastUpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();

        await RewardOPNsenseHelper.SetProfileDevicesBlockedAsync(
            _context, _opnsense, _logger, reward, blocked: false);

        TempData["Success"] = _localizer["Reward_Success_Resumed"].Value;
        return RedirectToAction("Home");
    }

    [HttpGet("challenges")]
    public IActionResult Challenges()
    {
        var profileId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var currentProfile = _context.Profiles.Find(profileId)!;

        var challenges = _context.Challenges
            .Include(c => c.Proofs)
            .Include(c => c.ParentProfile)
            .Where(c => c.ChildProfileId == profileId)
            .OrderByDescending(c => c.CreatedAt)
            .ToList();

        var parents = _context.Profiles
            .Where(p => p.Role == "parent")
            .OrderBy(p => p.DisplayName)
            .ToList();

        var vm = new ChildChallengesViewModel
        {
            CurrentProfile = currentProfile,
            MyChallenges = challenges,
            ParentProfiles = parents
        };

        return View(vm);
    }

    [HttpPost("challenges/submit/{id:int}")]
    public async Task<IActionResult> ChallengeSubmit(int id, string proofType, IFormFile? proofFile)
    {
        var profileId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var challenge = await _context.Challenges.FirstOrDefaultAsync(c => c.Id == id && c.ChildProfileId == profileId);

        if (challenge == null)
        {
            TempData["Error"] = _localizer["Challenge_Error_NotFound"].Value;
            return RedirectToAction("Challenges");
        }

        if (challenge.Status != "pending")
        {
            TempData["Error"] = _localizer["Challenge_Error_InvalidStatus"].Value;
            return RedirectToAction("Challenges");
        }

        string? filePath = null;

        if (proofType == "photo")
        {
            if (proofFile == null || proofFile.Length == 0)
            {
                TempData["Error"] = _localizer["Challenge_Error_NoProof"].Value;
                return RedirectToAction("Challenges");
            }

            var ext = Path.GetExtension(proofFile.FileName);
            if (!AllowedExtensions.Contains(ext))
            {
                TempData["Error"] = _localizer["Challenge_Error_FileType"].Value;
                return RedirectToAction("Challenges");
            }

            if (proofFile.Length > MaxFileSize)
            {
                TempData["Error"] = _localizer["Challenge_Error_FileSize"].Value;
                return RedirectToAction("Challenges");
            }

            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "proofs");
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(uploadsDir, fileName);

            using var stream = new FileStream(fullPath, FileMode.Create);
            await proofFile.CopyToAsync(stream);

            filePath = $"/uploads/proofs/{fileName}";
        }
        else if (proofType == "self_declared" && challenge.ProofRequired)
        {
            TempData["Error"] = _localizer["Challenge_Error_NoProof"].Value;
            return RedirectToAction("Challenges");
        }

        var proof = new ChallengeProof
        {
            ChallengeId = challenge.Id,
            ProofType = proofType,
            FilePath = filePath,
            SubmittedAt = DateTime.Now
        };

        challenge.Status = "submitted";

        _context.ChallengeProofs.Add(proof);
        await _context.SaveChangesAsync();

        TempData["Success"] = _localizer["Challenge_Success_Submitted"].Value;
        return RedirectToAction("Challenges");
    }

    [HttpPost("challenges/propose")]
    public async Task<IActionResult> ChallengePropose(string title, string? description, int rewardMinutes)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            TempData["Error"] = _localizer["Challenge_Error_Required"].Value;
            return RedirectToAction("Challenges");
        }

        var profileId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var currentProfile = _context.Profiles.Find(profileId)!;

        var firstParent = _context.Profiles.FirstOrDefault(p => p.Role == "parent");
        if (firstParent == null)
        {
            TempData["Error"] = _localizer["Challenge_Error_NotFound"].Value;
            return RedirectToAction("Challenges");
        }

        var challenge = new Challenge
        {
            ParentProfileId = firstParent.Id,
            ChildProfileId = profileId,
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            RewardMinutes = rewardMinutes > 0 ? rewardMinutes : 30,
            RewardScope = "global",
            ProofRequired = true,
            Status = "proposed",
            CreatedAt = DateTime.Now
        };

        _context.Challenges.Add(challenge);
        await _context.SaveChangesAsync();

        TempData["Success"] = _localizer["Challenge_Success_Proposed"].Value;
        return RedirectToAction("Challenges");
    }

    [HttpGet("devices")]
    public async Task<IActionResult> Devices()
    {
        var profileId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var now = DateTime.Now;

        var devices = await _context.Clients
            .Where(c => c.ProfileId == profileId)
            .ToListAsync();

        var activeReward = await _context.Rewards
            .Include(r => r.Challenge)
            .FirstOrDefaultAsync(r => r.ChildProfileId == profileId && r.Status == "active");

        var pausedReward = await _context.Rewards
            .Include(r => r.Challenge)
            .FirstOrDefaultAsync(r => r.ChildProfileId == profileId && r.Status == "paused");

        var activeTempAuth = await _context.TempAuthorizations
            .FirstOrDefaultAsync(t => t.ProfileId == profileId && t.ExpiresAt > now);

        var blockingSchedules = await _context.Schedules
            .Where(s => (s.ProfileId == profileId || s.ProfileId == null) && s.IsBlocking)
            .ToListAsync();

        double pct = 0;
        if (activeReward != null)
        {
            var total = activeReward.TotalMinutes * 60;
            var consumed = total - activeReward.RemainingSeconds;
            pct = total > 0 ? Math.Clamp(consumed * 100.0 / total, 0, 100) : 0;
        }

        bool isScheduleBlocking = activeReward == null
            && pausedReward == null
            && activeTempAuth == null
            && blockingSchedules.Any(s => IsCurrentlyInWindow(s, now));

        var vm = new DevicesViewModel
        {
            Devices           = devices,
            ActiveReward      = activeReward,
            PausedReward      = pausedReward,
            ActiveTempAuth    = activeTempAuth,
            BlockingSchedules = blockingSchedules,
            RewardProgressPct = pct,
            IsScheduleBlocking = isScheduleBlocking,
        };

        return View(vm);
    }

    private static bool IsCurrentlyInWindow(Schedule s, DateTime now)
    {
        var dayIndex = ((int)now.DayOfWeek + 6) % 7; // Mon=0 … Sun=6
        var currentTime = TimeOnly.FromDateTime(now);
        var dayActive = s.Days.Length >= 7 && s.Days[dayIndex] == '1';
        return dayActive && currentTime >= s.TimeStart && currentTime <= s.TimeEnd;
    }
}
