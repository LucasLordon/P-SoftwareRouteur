using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareRouteur.Data;
using SoftwareRouteur.Models;
using SoftwareRouteur.ViewModels;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace SoftwareRouteur.Controllers;

[Authorize]
public class FirewallController : Controller
{
    private readonly AppDbContext _context;

    public FirewallController(AppDbContext context)
    {
        _context = context;
    }

    // Liste des règles
    public IActionResult Index()
    {
        var vm = new FirewallIndexViewModel
        {
            Rules = _context.FirewallRules.Include(r => r.Client).ToList(),
            Clients = _context.Clients.ToList()
        };
        return View(vm);
    }

    // Ajouter une règle
    [HttpPost]
    public IActionResult Create(int clientId, string ruleType, string destination, string action)
    {
        var rule = new FirewallRule
        {
            ClientId = clientId,
            RuleType = ruleType,
            Destination = destination,
            Action = action,
            CreatedAt = DateTime.Now
        };
        _context.FirewallRules.Add(rule);
        _context.SaveChanges();
        //ApplyFirewallRules();
        return RedirectToAction("Index");
    }

    // Supprimer une règle
    [HttpPost]
    public IActionResult Delete(int id)
    {
        var rule = _context.FirewallRules.Find(id);
        if (rule != null)
        {
            _context.FirewallRules.Remove(rule);
            _context.SaveChanges();
            //ApplyFirewallRules();
        }
        return RedirectToAction("Index");
    }
    
    [HttpPost]
    public IActionResult Edit(int id, int clientId, string ruleType, string destination, string action)
    {
        var rule = _context.FirewallRules.Find(id);
        if (rule != null)
        {
            rule.ClientId = clientId;
            rule.RuleType = ruleType;
            rule.Destination = destination;
            rule.Action = action;
            _context.SaveChanges();
        }
        return RedirectToAction("Index");
    }

    private bool ApplyFirewallRules()
    {
        try
        {
            using var client = new SshClient("10.228.242.37", "admin", "admin");
            try
            {
                client.Connect();
            }
            catch (SshAuthenticationException ex)
            {
                Console.WriteLine("Auth failed: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Other error: " + ex.Message);
            }

            var cmd = client.RunCommand("sudo python3 /home/admin/sync_firewall.py");
            client.Disconnect();

            if (cmd.ExitStatus == 0)
            {
                TempData["Success"] = "Règles du pare-feu mises à jour avec succès.";
                return true;
            }
            else
            {
                TempData["Error"] = $"Erreur lors de la synchronisation : {cmd.Error}";
                return false;
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Impossible de se connecter à VM2 : {ex.Message}";
            return false;
        }
    }
}