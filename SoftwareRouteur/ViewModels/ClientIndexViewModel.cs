using SoftwareRouteur.Models;

namespace SoftwareRouteur.ViewModels;

public class ClientIndexViewModel
{
    public List<Client> Clients { get; set; } = new();
    public int TotalClients => Clients.Count;
}
