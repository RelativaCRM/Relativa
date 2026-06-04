using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class UserBackupCodeConfiguration : IEntityTypeConfiguration<UserBackupCode>
{
    public void Configure(EntityTypeBuilder<UserBackupCode> builder)
    {
        builder.ToTable("user_backup_codes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.CodeHash).HasColumnName("code_hash").HasMaxLength(128).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UsedAt).HasColumnName("used_at");
        builder.HasIndex(e => e.UserId).HasDatabaseName("ix_user_backup_codes_user_id");
        builder.HasOne(e => e.User)
            .WithMany(u => u.BackupCodes)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_backup_codes_user");
    }
}
