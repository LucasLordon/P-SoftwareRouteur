using System.ComponentModel.DataAnnotations.Schema;

namespace SoftwareRouteur.Models;

[Table("admin_users")]
public class AdminUser
{
    [Column("id")]
    public int Id { get; set; }
    [Column("username")]
    public required string Username { get; set; }
    [Column("password_hash")]
    public required string PasswordHash { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
