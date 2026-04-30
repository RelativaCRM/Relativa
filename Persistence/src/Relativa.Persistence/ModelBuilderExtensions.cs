using Microsoft.EntityFrameworkCore;
using Relativa.Persistence.Configurations;
using Relativa.Persistence.Configurations.AuditLogs;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence;

public static class PersistenceModelBuilderExtensions
{
    public static ModelBuilder ApplyAuthEntityConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new UserAuditLogConfiguration());

        // EF Core convention follows User's navigation properties into the full RBAC
        // graph. Cut both chains at the root so the Auth context stays User-only.
        modelBuilder.Ignore<UserRoleWorkspace>();
        modelBuilder.Ignore<UserRoleOrganization>();

        return modelBuilder;
    }

    public static ModelBuilder ApplyAllEntityConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionConfiguration());

        modelBuilder.ApplyConfiguration(new OrganizationConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationRoleConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationRolePermissionConfiguration());
        modelBuilder.ApplyConfiguration(new UserRoleOrganizationConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationJoinRequestConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationInvitationConfiguration());

        modelBuilder.ApplyConfiguration(new WorkspaceConfiguration());
        modelBuilder.ApplyConfiguration(new WorkspaceRoleConfiguration());
        modelBuilder.ApplyConfiguration(new WorkspaceRolePermissionConfiguration());
        modelBuilder.ApplyConfiguration(new UserRoleWorkspaceConfiguration());
        modelBuilder.ApplyConfiguration(new WorkspaceInvitationConfiguration());

        modelBuilder.ApplyConfiguration(new EntityTypeConfiguration());
        modelBuilder.ApplyConfiguration(new EntityConfiguration());
        modelBuilder.ApplyConfiguration(new EntityWorkspaceConfiguration());
        modelBuilder.ApplyConfiguration(new PropertyConfiguration());
        modelBuilder.ApplyConfiguration(new EntityTypePropertyConfiguration());
        modelBuilder.ApplyConfiguration(new EntityPropertyValueConfiguration());
        modelBuilder.ApplyConfiguration(new EntityRelationshipTypeConfiguration());
        modelBuilder.ApplyConfiguration(new EntityRelationshipConfiguration());

        modelBuilder.ApplyConfiguration(new EntityAuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new WorkspaceAuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new UserAuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationAuditLogConfiguration());

        return modelBuilder;
    }
}
