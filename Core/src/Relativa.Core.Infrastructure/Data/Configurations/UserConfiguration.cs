using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Core.Domain.Entities;

namespace Relativa.Core.Infrastructure.Data.Configurations;

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
        builder.HasIndex(e => e.Email).IsUnique();
        builder.Property(e => e.Password).HasColumnName("password").IsRequired();
        builder.Property(e => e.RoleId).HasColumnName("role_id").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.IsArchived).HasColumnName("is_archived").HasDefaultValue(false);
        builder.HasOne(e => e.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_users_role");
    }
}
