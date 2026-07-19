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
public DbSet<Page> Pages => Set<Page>();
    public DbSet<PageService> PageServices => Set<PageService>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentTimelineEvent> IncidentTimelineEvents => Set<IncidentTimelineEvent>();
    public DbSet<IncidentService> IncidentServices => Set<IncidentService>();
    public DbSet<IncidentImpactChange> IncidentImpactChanges => Set<IncidentImpactChange>();
    public DbSet<Maintenance> Maintenances => Set<Maintenance>();
    public DbSet<MaintenanceEvent> MaintenanceEvents => Set<MaintenanceEvent>();
    public DbSet<MaintenanceService> MaintenanceServices => Set<MaintenanceService>();
    public DbSet<AlertConfig> AlertConfigs => Set<AlertConfig>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<EscalationDeliveryLog> EscalationDeliveryLogs => Set<EscalationDeliveryLog>();
    public DbSet<WebhookRequestLog> WebhookRequestLogs => Set<WebhookRequestLog>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<SiteData> SiteData => Set<SiteData>();
    public DbSet<WorkerRegistration> WorkerRegistrations => Set<WorkerRegistration>();
    public DbSet<PiroLog> PiroLogs => Set<PiroLog>();
    public DbSet<OidcProviderConfig> OidcProviderConfigs => Set<OidcProviderConfig>();
    public DbSet<Integration> Integrations => Set<Integration>();
    public DbSet<OAuthToken> OAuthTokens => Set<OAuthToken>();
    public DbSet<ServiceIntegrationMapping> ServiceIntegrationMappings => Set<ServiceIntegrationMapping>();

    // On-call scheduling
    public DbSet<OnCallSchedule> OnCallSchedules => Set<OnCallSchedule>();
    public DbSet<OnCallLayer> OnCallLayers => Set<OnCallLayer>();
    public DbSet<OnCallLayerUser> OnCallLayerUsers => Set<OnCallLayerUser>();
    public DbSet<OnCallOverride> OnCallOverrides => Set<OnCallOverride>();
    public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();

    // Escalation
    public DbSet<EscalationPolicy> EscalationPolicies => Set<EscalationPolicy>();
    public DbSet<EscalationStep> EscalationSteps => Set<EscalationStep>();

    // Notification push engine (RFC 0009)
    public DbSet<NotificationEventOutbox> NotificationEventOutbox => Set<NotificationEventOutbox>();
    public DbSet<NotificationDeliveryLog> NotificationDeliveryLogs => Set<NotificationDeliveryLog>();

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
