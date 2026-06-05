using Microsoft.EntityFrameworkCore;
using Relativa.Persistence;
using Relativa.Persistence.Entities;
using Relativa.Persistence.Entities.AuditLogs;

namespace Relativa.Authentication.Infrastructure.Data;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();
    public DbSet<UserBackupCode> UserBackupCodes => Set<UserBackupCode>();
    public DbSet<UserEmail> UserEmails => Set<UserEmail>();
    public DbSet<UserAuditLog> UserAuditLogs => Set<UserAuditLog>();
    public DbSet<AuditOutboxMessage> AuditOutboxMessages => Set<AuditOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyAuthEntityConfigurations();
    }
}
