using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SoftwareRouteur.Models;

[Table("profiles")]
public class Profile
{
    [Column("id")]
    public int Id { get; set; }

    [Column("display_name")]
    [MaxLength(100)]
    public required string DisplayName { get; set; }

    [Column("role")]
    public required string Role { get; set; }

    [Column("pin_hash")]
    public string? PinHash { get; set; }

    [Column("created_by_id")]
    public int? CreatedById { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Profile? CreatedBy { get; set; }
    public ICollection<Client> Clients { get; set; } = new List<Client>();
}
