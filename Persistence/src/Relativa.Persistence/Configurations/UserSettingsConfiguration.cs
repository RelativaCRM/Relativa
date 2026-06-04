using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("user_settings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.Locale)
            .HasColumnName("locale")
            .HasMaxLength(10)
            .HasDefaultValue("en")
            .IsRequired();
        builder.HasIndex(e => e.UserId)
            .IsUnique()
            .HasDatabaseName("ix_user_settings_user_id");
        builder.HasOne(e => e.User)
            .WithOne(u => u.Settings)
            .HasForeignKey<UserSettings>(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_settings_user");
    }
}
