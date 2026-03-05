using System.ComponentModel.DataAnnotations.Schema;

namespace SoftwareRouteur.Models;

[Table("monitoring")]
public class Monitoring
{
    [Column("id")]
    public int Id { get; set; }

    [Column("client_ip")]
    public string ClientIp { get; set; }

    [Column("hostname")]
    public string? Hostname { get; set; }

    [Column("is_online")]
    public bool IsOnline { get; set; }

    [Column("active_rules")]
    public int ActiveRules { get; set; }

    [Column("checked_at")]
    public DateTime CheckedAt { get; set; }
}
