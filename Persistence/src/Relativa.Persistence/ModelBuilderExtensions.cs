using Microsoft.EntityFrameworkCore;
using Relativa.Persistence.Configurations;

namespace Relativa.Persistence;

public static class PersistenceModelBuilderExtensions
{
    public static ModelBuilder ApplyAuthEntityConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionConfiguration());
        modelBuilder.ApplyConfiguration(new RolePermissionConfiguration());
        return modelBuilder;
    }

    public static ModelBuilder ApplyAllEntityConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyAuthEntityConfigurations();
        modelBuilder.ApplyConfiguration(new OrganizationConfiguration());
        modelBuilder.ApplyConfiguration(new WorkspaceConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationWorkspaceConfiguration());
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
