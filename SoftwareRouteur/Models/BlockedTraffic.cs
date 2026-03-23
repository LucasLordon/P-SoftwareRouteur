using System.ComponentModel.DataAnnotations.Schema;

namespace SoftwareRouteur.Models;

[Table("blocked_traffic")]
public class BlockedTraffic
{
    [Column("id")]
    public int Id { get; set; }

    [Column("src_ip")]
    public required string SrcIp { get; set; }

    [Column("dst_ip")]
    public required string DstIp { get; set; }

    [Column("logged_at")]
    public DateTime LoggedAt { get; set; }
}
