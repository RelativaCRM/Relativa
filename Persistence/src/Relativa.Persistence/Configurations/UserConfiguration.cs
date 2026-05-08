using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.FirstName).HasColumnName("first_name").IsRequired();
        builder.Property(e => e.LastName).HasColumnName("last_name").IsRequired();
        builder.Property(e => e.Email).HasColumnName("email").IsRequired();
        builder.HasIndex(e => e.Email)
            .IsUnique()
            .HasFilter("\"is_archived\" = FALSE");
        builder.Property(e => e.Password).HasColumnName("password").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.IsArchived).HasColumnName("is_archived").HasDefaultValue(false);
        builder.Property(e => e.PasswordResetToken).HasColumnName("password_reset_token");
        builder.Property(e => e.PasswordResetTokenExpiresAt).HasColumnName("password_reset_token_expires_at");
    }
}
