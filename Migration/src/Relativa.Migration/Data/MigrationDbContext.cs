using Microsoft.EntityFrameworkCore;
using Relativa.Migration.Models;

namespace Relativa.Migration.Data;

public sealed class MigrationDbContext(DbContextOptions<MigrationDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<EntityType> EntityTypes => Set<EntityType>();
    public DbSet<PersonalDataPropertyValue> PersonalDataPropertyValues => Set<PersonalDataPropertyValue>();
    public DbSet<LocationPropertyValue> LocationPropertyValues => Set<LocationPropertyValue>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<User> Users => Set<User>();
    public DbSet<OrganizationWorkspace> OrganizationWorkspaces => Set<OrganizationWorkspace>();
    public DbSet<Entity> Entities => Set<Entity>();
    public DbSet<EntityWorkspace> EntityWorkspaces => Set<EntityWorkspace>();
    public DbSet<DealPropertyValue> DealPropertyValues => Set<DealPropertyValue>();
    public DbSet<EntityProperty> EntityProperties => Set<EntityProperty>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ÐÐ²Ñ‚Ð¾Ð¼Ð°Ñ‚Ð¸Ñ‡Ð½Ð¾ Ð·Ð°ÑÑ‚Ð¾ÑÐ¾Ð²ÑƒÑ” Ð²ÑÑ– IEntityTypeConfiguration Ð· Ñ†Ñ–Ñ”Ñ— Ð·Ð±Ñ–Ñ€ÐºÐ¸
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MigrationDbContext).Assembly);
    }
}
