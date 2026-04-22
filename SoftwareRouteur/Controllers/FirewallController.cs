using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SoftwareRouteur.Data;
using SoftwareRouteur.Models;
using SoftwareRouteur.Services;
using SoftwareRouteur.ViewModels;

namespace SoftwareRouteur.Controllers;

[Authorize]
public class FirewallController : Controller
{
    private readonly AppDbContext _context;
    private readonly IStringLocalizer<FirewallController> _localizer;
    private readonly OPNsenseService _opnsense;
    private readonly ILogger<FirewallController> _logger;

    public FirewallController(AppDbContext context, IStringLocalizer<FirewallController> localizer, OPNsenseService opnsense, ILogger<FirewallController> logger)
    {
        _context = context;
        _localizer = localizer;
        _opnsense = opnsense;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var vm = new FirewallIndexViewModel
        {
            Rules = _context.FirewallRules.Include(r => r.Client).ToList(),
            Clients = _context.Clients.ToList()
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int clientId, string ruleType, string destination, string action)
    {
        _logger.LogDebug("Create rule — clientId={ClientId}, ruleType={RuleType}, destination={Destination}, action={Action}", clientId, ruleType, destination, action);

        if (!_context.Clients.Any(c => c.Id == clientId))
        {
            TempData["Error"] = _localizer["Error_InvalidClient"].Value;
            return RedirectToAction("Index");
        }

        var rule = new FirewallRule
        {
            ClientId = clientId,
            RuleType = ruleType,
            Destination = destination,
            Action = action,
            CreatedAt = DateTime.Now
        };
        _context.FirewallRules.Add(rule);
        await _context.SaveChangesAsync();
        
        _logger.LogDebug("Condition OPNsense — action == 'deny' ? {Result} (valeur reçue: '{Action}')", action == "deny", action);
        if (action == "deny")
        {
            _logger.LogInformation("Appel OPNsense AddToAliasAsync — clientId={ClientId}, destination={Destination}", clientId, destination);
            await _opnsense.AddToAliasAsync(clientId, destination);
        }
        else
        {
            _logger.LogWarning("Appel OPNsense IGNORÉ — action='{Action}' ne correspond pas à 'deny'", action);
        }

        TempData["Success"] = _localizer["Success_Created"].Value;
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var rule = _context.FirewallRules.Find(id);
        _logger.LogDebug("Delete rule id={Id} — action='{Action}', clientId={ClientId}, destination={Destination}", id, rule?.Action, rule?.ClientId, rule?.Destination);
        if (rule != null)
        {
            _logger.LogDebug("Condition OPNsense — action == 'deny' ? {Result} (valeur reçue: '{Action}')", rule.Action == "deny", rule.Action);
            if (rule.Action == "deny")
            {
                _logger.LogInformation("Appel OPNsense RemoveFromAliasAsync — clientId={ClientId}, destination={Destination}", rule.ClientId, rule.Destination);
                await _opnsense.RemoveFromAliasAsync(rule.ClientId, rule.Destination);
            }
            else
            {
                _logger.LogWarning("Appel OPNsense IGNORÉ — action='{Action}' ne correspond pas à 'deny'", rule.Action);
            }

            _context.FirewallRules.Remove(rule);
            await _context.SaveChangesAsync();
            TempData["Success"] = _localizer["Success_Deleted"].Value;
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, int clientId, string ruleType, string destination, string action)
    {
        var rule = _context.FirewallRules.Find(id);
        _logger.LogDebug("Edit rule id={Id} — ancien état: action='{OldAction}', clientId={OldClientId}, destination='{OldDestination}'", id, rule?.Action, rule?.ClientId, rule?.Destination);
        if (rule != null)
        {
            _logger.LogDebug("Condition OPNsense (ancien état) — action == 'deny' ? {Result} (valeur reçue: '{Action}')", rule.Action == "deny", rule.Action);
            if (rule.Action == "deny")
            {
                _logger.LogInformation("Appel OPNsense RemoveFromAliasAsync (ancien état) — clientId={ClientId}, destination={Destination}", rule.ClientId, rule.Destination);
                await _opnsense.RemoveFromAliasAsync(rule.ClientId, rule.Destination);
            }
            else
            {
                _logger.LogWarning("Appel OPNsense IGNORÉ (ancien état) — action='{Action}' ne correspond pas à 'deny'", rule.Action);
            }

            rule.ClientId = clientId;
            rule.RuleType = ruleType;
            rule.Destination = destination;
            rule.Action = action;
            await _context.SaveChangesAsync();
            
            _logger.LogDebug("Edit rule id={Id} — nouvel état: action='{Action}', clientId={ClientId}, destination='{Destination}'", id, action, clientId, destination);
            _logger.LogDebug("Condition OPNsense (nouvel état) — action == 'deny' ? {Result} (valeur reçue: '{Action}')", action == "deny", action);
            if (action == "deny")
            {
                _logger.LogInformation("Appel OPNsense AddToAliasAsync (nouvel état) — clientId={ClientId}, destination={Destination}", clientId, destination);
                await _opnsense.AddToAliasAsync(clientId, destination);
            }
            else
            {
                _logger.LogWarning("Appel OPNsense IGNORÉ (nouvel état) — action='{Action}' ne correspond pas à 'deny'", action);
            }

            TempData["Success"] = _localizer["Success_Updated"].Value;
        }
        return RedirectToAction("Index");
    }
}