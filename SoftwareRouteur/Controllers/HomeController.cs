using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareRouteur.Data;
using SoftwareRouteur.ViewModels;

namespace SoftwareRouteur.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly AppDbContext _context;

    public HomeController(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        var clients = _context.Clients.ToList();

        // Dernier statut de chaque IP
        var clientIps = clients.Select(c => c.IpAddress).ToList();
        var latestStatuses = _context.Monitorings
            .Where(m => clientIps.Contains(m.ClientIp))
            .Select(m => new { m.ClientIp, m.IsOnline, m.CheckedAt })
            .AsEnumerable()
            .GroupBy(m => m.ClientIp)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(m => m.CheckedAt).First().IsOnline
            );

        var clientStatuses = clients.ToDictionary(
            c => c.Id,
            c => latestStatuses.GetValueOrDefault(c.IpAddress, false)
        );

        // Nombre de règles
        var firewallRules = _context.FirewallRules;
        int activeRules = firewallRules.Count();

        // Blocages aujourd'hui
        var today = DateTime.Today;
        int blockedToday = _context.BlockedTraffics
            .Count(b => b.LoggedAt >= today);

        var vm = new DashboardViewModel
        {
            Clients = clients,
            ClientStatuses = clientStatuses,
            TotalClients = clients.Count,
            OnlineClients = clientStatuses.Count(s => s.Value),
            ActiveRules = activeRules,
            BlockedToday = blockedToday
        };

        return View(vm);
    }
}