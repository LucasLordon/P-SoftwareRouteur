using SoftwareRouteur.Models;

namespace SoftwareRouteur.ViewModels;

public class DevicesViewModel
{
    public List<Client> Devices { get; set; } = new();
    public Reward? ActiveReward { get; set; }
    public Reward? PausedReward { get; set; }
    public TempAuthorization? ActiveTempAuth { get; set; }
    public List<Schedule> BlockingSchedules { get; set; } = new();
    public double RewardProgressPct { get; set; }
    public bool IsScheduleBlocking { get; set; }
}
