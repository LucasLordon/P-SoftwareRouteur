using SoftwareRouteur.Models;

namespace SoftwareRouteur.ViewModels;

public class ChildHomeViewModel
{
    public Profile CurrentProfile { get; set; } = null!;
    public List<Client> AssignedClients { get; set; } = new();
}
