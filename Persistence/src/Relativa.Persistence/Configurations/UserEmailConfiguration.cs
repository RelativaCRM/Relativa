using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class UserEmailConfiguration : IEntityTypeConfiguration<UserEmail>
{
    public void Configure(EntityTypeBuilder<UserEmail> builder)
    {
        builder.ToTable("user_emails");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.Address).HasColumnName("address").HasMaxLength(256).IsRequired();
        builder.Property(e => e.IsVerified).HasColumnName("is_verified").HasDefaultValue(false);
        builder.Property(e => e.Source).HasColumnName("source").HasMaxLength(32).IsRequired();
        builder.Property(e => e.VerificationToken).HasColumnName("verification_token");
        builder.Property(e => e.VerificationTokenExpiresAt).HasColumnName("verification_token_expires_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasIndex(e => e.Address).IsUnique().HasDatabaseName("ix_user_emails_address");
        builder.HasOne(e => e.User)
            .WithMany(u => u.Emails)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_emails_user");
    }
}
