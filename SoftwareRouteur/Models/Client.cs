using System.ComponentModel.DataAnnotations.Schema;

namespace SoftwareRouteur.Models;

[Table("clients")]
public class Client
{
    [Column("id")]
    public int Id { get; set; }
    
    [Column("hostname")]
    public required string Hostname { get; set; }

    [Column("ip_address")]
    public required string IpAddress { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public List<FirewallRule> FirewallRules { get; set; } = new();
    
    [Column("opnsense_alias_uuid")]
    public string? OpnsenseAliasUuid { get; set; }

    [Column("opnsense_rule_uuid")]
    public string? OpnsenseRuleUuid { get; set; }
}
