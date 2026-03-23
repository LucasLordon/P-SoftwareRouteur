using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using SoftwareRouteur.Data;
using SoftwareRouteur.Models;

namespace SoftwareRouteur.Controllers;

[Authorize]
public class ClientController : Controller
{
    private readonly AppDbContext _context;
    private readonly IStringLocalizer<ClientController> _localizer;

    public ClientController(AppDbContext context, IStringLocalizer<ClientController> localizer)
    {
        _context = context;
        _localizer = localizer;
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
        _context.SaveChanges();
        TempData["Success"] = string.Format(_localizer["Success_Created"].Value, hostname);
        return RedirectToAction("Index");
    }

    [HttpPost]
    public IActionResult Edit(int id, string hostname, string ipAddress)
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
            _context.SaveChanges();
            TempData["Success"] = string.Format(_localizer["Success_Updated"].Value, hostname);
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
            TempData["Success"] = string.Format(_localizer["Success_Deleted"].Value, client.Hostname);
        }
        return RedirectToAction("Index");
    }
}
