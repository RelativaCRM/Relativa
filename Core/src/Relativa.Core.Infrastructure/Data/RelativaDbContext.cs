using Microsoft.EntityFrameworkCore;
using Relativa.Persistence;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Data;

public sealed class RelativaDbContext(DbContextOptions<RelativaDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<OrganizationWorkspace> OrganizationWorkspaces => Set<OrganizationWorkspace>();
    public DbSet<EntityType> EntityTypes => Set<EntityType>();
    public DbSet<Entity> Entities => Set<Entity>();
    public DbSet<EntityWorkspace> EntityWorkspaces => Set<EntityWorkspace>();
    public DbSet<PersonalDataPropertyValue> PersonalDataPropertyValues => Set<PersonalDataPropertyValue>();
    public DbSet<LocationPropertyValue> LocationPropertyValues => Set<LocationPropertyValue>();
    public DbSet<DealPropertyValue> DealPropertyValues => Set<DealPropertyValue>();
    public DbSet<EntityProperty> EntityProperties => Set<EntityProperty>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyAllEntityConfigurations();
    }
}
