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
    public DbSet<Profile> Profiles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Profile>()
            .HasOne(p => p.CreatedBy)
            .WithMany()
            .HasForeignKey(p => p.CreatedById)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Client>()
            .HasOne(c => c.Profile)
            .WithMany(p => p.Clients)
            .HasForeignKey(c => c.ProfileId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
