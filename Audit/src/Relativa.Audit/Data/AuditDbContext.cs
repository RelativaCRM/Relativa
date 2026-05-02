using Microsoft.EntityFrameworkCore;
using Relativa.Persistence;
using Relativa.Persistence.Entities;
using Relativa.Persistence.Entities.AuditLogs;

namespace Relativa.Audit.Data;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<EntityAuditLog> EntityAuditLogs => Set<EntityAuditLog>();
    public DbSet<WorkspaceAuditLog> WorkspaceAuditLogs => Set<WorkspaceAuditLog>();
    public DbSet<OrganizationAuditLog> OrganizationAuditLogs => Set<OrganizationAuditLog>();
    public DbSet<UserAuditLog> UserAuditLogs => Set<UserAuditLog>();
    public DbSet<AuditProcessedEvent> AuditProcessedEvents => Set<AuditProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyAllEntityConfigurations();
    }
}
