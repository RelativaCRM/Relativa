using Microsoft.EntityFrameworkCore;
using Relativa.Persistence;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Data;

public sealed class RelativaDbContext(DbContextOptions<RelativaDbContext> options) : DbContext(options)
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
    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();

    public DbSet<EntityType> EntityTypes => Set<EntityType>();
    public DbSet<Entity> Entities => Set<Entity>();
    public DbSet<EntityWorkspace> EntityWorkspaces => Set<EntityWorkspace>();
    public DbSet<PersonalDataPropertyValue> PersonalDataPropertyValues => Set<PersonalDataPropertyValue>();
    public DbSet<LocationPropertyValue> LocationPropertyValues => Set<LocationPropertyValue>();
    public DbSet<DealPropertyValue> DealPropertyValues => Set<DealPropertyValue>();
    public DbSet<EntityProperty> EntityProperties => Set<EntityProperty>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyAllEntityConfigurations();
    }
}
