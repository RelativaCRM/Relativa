using Microsoft.EntityFrameworkCore;
using Relativa.Persistence;
using Relativa.Persistence.Entities;
using Relativa.Persistence.Entities.AuditLogs;

namespace Relativa.Migration.Data;

public sealed class MigrationDbContext(DbContextOptions<MigrationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationRole> OrganizationRoles => Set<OrganizationRole>();
    public DbSet<OrganizationRolePermission> OrganizationRolePermissions => Set<OrganizationRolePermission>();
    public DbSet<UserRoleOrganization> UserRoleOrganizations => Set<UserRoleOrganization>();
    public DbSet<OrganizationJoinRequest> OrganizationJoinRequests => Set<OrganizationJoinRequest>();
    public DbSet<OrganizationInvitation> OrganizationInvitations => Set<OrganizationInvitation>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceRole> WorkspaceRoles => Set<WorkspaceRole>();
    public DbSet<WorkspaceRolePermission> WorkspaceRolePermissions => Set<WorkspaceRolePermission>();
    public DbSet<UserRoleWorkspace> UserRoleWorkspaces => Set<UserRoleWorkspace>();

    public DbSet<EntityType> EntityTypes => Set<EntityType>();
    public DbSet<Entity> Entities => Set<Entity>();
    public DbSet<EntityWorkspace> EntityWorkspaces => Set<EntityWorkspace>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<EntityTypeProperty> EntityTypeProperties => Set<EntityTypeProperty>();
    public DbSet<EntityPropertyValue> EntityPropertyValues => Set<EntityPropertyValue>();
    public DbSet<EntityRelationshipType> EntityRelationshipTypes => Set<EntityRelationshipType>();
    public DbSet<EntityRelationship> EntityRelationships => Set<EntityRelationship>();
    public DbSet<EntityAuditLog> EntityAuditLogs => Set<EntityAuditLog>();
    public DbSet<WorkspaceAuditLog> WorkspaceAuditLogs => Set<WorkspaceAuditLog>();
    public DbSet<UserAuditLog> UserAuditLogs => Set<UserAuditLog>();
    public DbSet<OrganizationAuditLog> OrganizationAuditLogs => Set<OrganizationAuditLog>();
    public DbSet<AuditOutboxMessage> AuditOutboxMessages => Set<AuditOutboxMessage>();
    public DbSet<AuditProcessedEvent> AuditProcessedEvents => Set<AuditProcessedEvent>();
    public DbSet<RabbitMqProcessedDelivery> RabbitMqProcessedDeliveries => Set<RabbitMqProcessedDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyAllEntityConfigurations();
    }
}
