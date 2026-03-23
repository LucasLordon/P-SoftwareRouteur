using System.ComponentModel.DataAnnotations.Schema;

namespace SoftwareRouteur.Models;

[Table("firewall_rules")]
public class FirewallRule
{
    [Column("id")]
    public int Id { get; set; }
    [Column("rule_type")]
    public required string RuleType { get; set; }
    [Column("destination")]
    public required string Destination { get; set; }
    [Column("action")]
    public required string Action { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    [Column("client_id")]
    public int ClientId { get; set; }
    public Client? Client { get; set; }
}
