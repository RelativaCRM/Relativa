using Microsoft.EntityFrameworkCore;
using Relativa.Persistence.Configurations;

namespace Relativa.Persistence;

public static class PersistenceModelBuilderExtensions
{
    public static ModelBuilder ApplyAuthEntityConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionConfiguration());
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
        modelBuilder.ApplyConfiguration(new PersonalDataPropertyValueConfiguration());
        modelBuilder.ApplyConfiguration(new LocationPropertyValueConfiguration());
        modelBuilder.ApplyConfiguration(new DealPropertyValueConfiguration());
        modelBuilder.ApplyConfiguration(new EntityPropertyConfiguration());
        return modelBuilder;
    }
}
