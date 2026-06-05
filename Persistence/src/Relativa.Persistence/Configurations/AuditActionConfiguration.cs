using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class AuditActionConfiguration : IEntityTypeConfiguration<AuditAction>
{
    public void Configure(EntityTypeBuilder<AuditAction> builder)
    {
        builder.ToTable("audit_action");
        builder.HasKey(a => a.Name);
        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(a => a.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
    }
}
