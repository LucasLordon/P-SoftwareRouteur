using SoftwareRouteur.Models;

namespace SoftwareRouteur.ViewModels;

public class ClientIndexViewModel
{
    public List<Client> Clients { get; set; } = new();
    public int TotalClients => Clients.Count;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
