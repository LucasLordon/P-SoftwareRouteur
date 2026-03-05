using Microsoft.EntityFrameworkCore;
using SoftwareRouteur.Models;

namespace SoftwareRouteur.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Client> Clients { get; set; }
    public DbSet<FirewallRule> FirewallRules { get; set; }
    public DbSet<AdminUser> AdminUsers { get; set; }
    public DbSet<Monitoring> Monitorings { get; set; }
    public DbSet<BlockedTraffic> BlockedTraffics { get; set; }

}