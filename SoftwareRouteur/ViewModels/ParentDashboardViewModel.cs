using SoftwareRouteur.Models;

namespace SoftwareRouteur.ViewModels;

public class ParentDashboardViewModel
{
    public Profile CurrentProfile { get; set; } = null!;
    public List<ChildSummary> ChildProfiles { get; set; } = new();
}

public class ChildSummary
{
    public Profile Profile { get; set; } = null!;
    public int DeviceCount { get; set; }
}
