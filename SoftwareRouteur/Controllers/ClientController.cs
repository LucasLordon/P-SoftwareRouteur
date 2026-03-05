using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareRouteur.Data;
using SoftwareRouteur.Models;

namespace SoftwareRouteur.Controllers;

[Authorize]
public class ClientController : Controller
{
    private readonly AppDbContext _context;

    public ClientController(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        var clients = _context.Clients.ToList();
        return View(clients);
    }

    [HttpPost]
    public IActionResult Create(string hostname, string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(hostname) || string.IsNullOrWhiteSpace(ipAddress))
        {
            TempData["Error"] = "Le hostname et l'adresse IP sont requis.";
            return RedirectToAction("Index");
        }

        var client = new Client
        {
            Hostname = hostname,
            IpAddress = ipAddress,
            CreatedAt = DateTime.Now
        };
        _context.Clients.Add(client);
        _context.SaveChanges();
        TempData["Success"] = $"Client « {hostname} » ajouté avec succès.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public IActionResult Edit(int id, string hostname, string ipAddress)
    {
        var client = _context.Clients.Find(id);
        if (client != null)
        {
            client.Hostname = hostname;
            client.IpAddress = ipAddress;
            _context.SaveChanges();
            TempData["Success"] = $"Client « {hostname} » modifié avec succès.";
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        var client = _context.Clients.Find(id);
        if (client != null)
        {
            _context.Clients.Remove(client);
            _context.SaveChanges();
            TempData["Success"] = $"Client « {client.Hostname} » supprimé.";
        }
        return RedirectToAction("Index");
    }
}
