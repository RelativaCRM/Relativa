using Microsoft.EntityFrameworkCore;
using Relativa.Persistence;
using Relativa.Persistence.Entities;

namespace Relativa.Authentication.Infrastructure.Data;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Permission> Permissions => Set<Permission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyAuthEntityConfigurations();
    }
}
