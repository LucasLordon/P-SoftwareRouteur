using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using SoftwareRouteur.Data;
using SoftwareRouteur.Models;
using SoftwareRouteur.Services;
using SoftwareRouteur.ViewModels;

namespace SoftwareRouteur.Controllers;

[Authorize]
public class ClientController : Controller
{
    private readonly AppDbContext _context;
    private readonly IStringLocalizer<ClientController> _localizer;
    private readonly OPNsenseService _opnsense;
    private readonly ILogger<ClientController> _logger;

    public ClientController(AppDbContext context, IStringLocalizer<ClientController> localizer, OPNsenseService opnsense, ILogger<ClientController> logger)
    {
        _context = context;
        _localizer = localizer;
        _opnsense = opnsense;
        _logger = logger;
    }

    public IActionResult Index(int page = 1, int pageSize = 10)
    {
        var totalCount = _context.Clients.Count();
        var clients = _context.Clients
            .OrderBy(c => c.Hostname)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var vm = new ClientIndexViewModel
        {
            Clients = clients,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(string hostname, string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(hostname) || string.IsNullOrWhiteSpace(ipAddress))
        {
            TempData["Error"] = _localizer["Error_Required"].Value;
            return RedirectToAction("Index");
        }

        if (!IPAddress.TryParse(ipAddress, out _))
        {
            TempData["Error"] = string.Format(_localizer["Error_InvalidIp"].Value, ipAddress);
            return RedirectToAction("Index");
        }

        var client = new Client
        {
            Hostname = hostname,
            IpAddress = ipAddress,
            CreatedAt = DateTime.Now
        };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var aliasUuid = await _opnsense.CreateAliasAsync(
            client.Id,
            $"Blocklist pour {client.Hostname}");
        client.OpnsenseAliasUuid = aliasUuid;

        var ruleUuid = await _opnsense.CreateBlockRuleAsync(
            client.Id,
            client.IpAddress,
            $"Block {client.Hostname}");
        client.OpnsenseRuleUuid = ruleUuid;

        var whitelistUuid = await _opnsense.CreateWhitelistAliasAsync(
            client.Id,
            $"Whitelist pour {client.Hostname}");
        client.OpnsenseWhitelistUuid = whitelistUuid;

        var allowRuleUuid = await _opnsense.CreateAllowRuleAsync(
            client.Id,
            client.IpAddress,
            $"Allow {client.Hostname}");
        client.OpnsenseAllowRuleUuid = allowRuleUuid;

        await _context.SaveChangesAsync();

        TempData["Success"] = string.Format(_localizer["Success_Created"].Value, hostname);
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, string hostname, string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(hostname) || string.IsNullOrWhiteSpace(ipAddress))
        {
            TempData["Error"] = _localizer["Error_Required"].Value;
            return RedirectToAction("Index");
        }

        if (!IPAddress.TryParse(ipAddress, out _))
        {
            TempData["Error"] = string.Format(_localizer["Error_InvalidIp"].Value, ipAddress);
            return RedirectToAction("Index");
        }

        var client = _context.Clients.Find(id);
        if (client != null)
        {
            client.Hostname = hostname;
            client.IpAddress = ipAddress;
            await _context.SaveChangesAsync();

            if (client.OpnsenseAliasUuid != null)
            {
                var ok = await _opnsense.UpdateAliasDescriptionAsync(
                    client.OpnsenseAliasUuid,
                    $"Blocklist pour {client.Hostname}");
                if (!ok)
                    _logger.LogWarning("Échec mise à jour description alias pour client {Id}", client.Id);
            }

            TempData["Success"] = string.Format(_localizer["Success_Updated"].Value, hostname);
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var client = _context.Clients.Find(id);
        if (client != null)
        {
            if (client.OpnsenseRuleUuid != null)
                await _opnsense.DeleteFirewallRuleAsync(client.OpnsenseRuleUuid);

            if (client.OpnsenseAllowRuleUuid != null)
                await _opnsense.DeleteFirewallRuleAsync(client.OpnsenseAllowRuleUuid);

            await _opnsense.DeleteAliasAsync(client.Id);
            await _opnsense.DeleteWhitelistAliasAsync(client.Id);

            _context.Clients.Remove(client);
            await _context.SaveChangesAsync();
            TempData["Success"] = string.Format(_localizer["Success_Deleted"].Value, client.Hostname);
        }
        return RedirectToAction("Index");
    }
}