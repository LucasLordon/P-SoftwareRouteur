using SoftwareRouteur.Models;

namespace SoftwareRouteur.ViewModels;

public class DashboardViewModel
{
    public List<Client> Clients { get; set; } = new();
    public Dictionary<int, bool> ClientStatuses { get; set; } = new();
    public int TotalClients { get; set; }
    public int OnlineClients { get; set; }
    public int ActiveRules { get; set; }
    public int BlockedToday { get; set; }
}
