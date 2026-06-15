using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence;

/// <summary>
/// EF Core database context for Piro.
/// Extends <see cref="IdentityDbContext{TUser,TRole,TKey}"/> so that ASP.NET Core Identity
/// manages the user, role, and claim tables alongside the Piro domain tables.
/// </summary>
public class PiroDbContext(DbContextOptions<PiroDbContext> options)
    : IdentityDbContext<AppUser, AppRole, int,
        IdentityUserClaim<int>, IdentityUserRole<int>, IdentityUserLogin<int>,
        IdentityRoleClaim<int>, IdentityUserToken<int>>(options)
{
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ServiceDependency> ServiceDependencies => Set<ServiceDependency>();
    public DbSet<Check> Checks => Set<Check>();
    public DbSet<CheckDataPoint> CheckDataPoints => Set<CheckDataPoint>();
    public DbSet<ServiceStatusSnapshot> ServiceStatusSnapshots => Set<ServiceStatusSnapshot>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<PageService> PageServices => Set<PageService>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentComment> IncidentComments => Set<IncidentComment>();
    public DbSet<IncidentService> IncidentServices => Set<IncidentService>();
    public DbSet<Maintenance> Maintenances => Set<Maintenance>();
    public DbSet<MaintenanceEvent> MaintenanceEvents => Set<MaintenanceEvent>();
    public DbSet<MaintenanceService> MaintenanceServices => Set<MaintenanceService>();
    public DbSet<Trigger> Triggers => Set<Trigger>();
    public DbSet<AlertConfig> AlertConfigs => Set<AlertConfig>();
    public DbSet<AlertConfigTrigger> AlertConfigTriggers => Set<AlertConfigTrigger>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<SiteData> SiteData => Set<SiteData>();
    public DbSet<WorkerRegistration> WorkerRegistrations => Set<WorkerRegistration>();
    public DbSet<PiroLog> PiroLogs => Set<PiroLog>();
    public DbSet<OidcProviderConfig> OidcProviderConfigs => Set<OidcProviderConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity must configure its tables before our custom configurations run
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PiroDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        SetAuditTimestamps();
        return base.SaveChanges();
    }

    private void SetAuditTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Properties.Any(p => p.Metadata.Name == "CreatedAt"))
                entry.Property("CreatedAt").CurrentValue = now;

            if (entry.State is EntityState.Added or EntityState.Modified && entry.Properties.Any(p => p.Metadata.Name == "UpdatedAt"))
                entry.Property("UpdatedAt").CurrentValue = now;
        }
    }
}
