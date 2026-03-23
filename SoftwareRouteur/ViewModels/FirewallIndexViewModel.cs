using SoftwareRouteur.Models;

namespace SoftwareRouteur.ViewModels;

public class FirewallIndexViewModel
{
    public List<FirewallRule> Rules { get; set; } = new();
    public List<Client> Clients { get; set; } = new();
}
