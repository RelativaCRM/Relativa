using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Migration.Models;
namespace Relativa.Migration.Configurations;
public class DealPropertyValueConfiguration : IEntityTypeConfiguration<DealPropertyValue> {
    public void Configure(EntityTypeBuilder<DealPropertyValue> builder) {
        builder.ToTable("deal_property_values");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Value).HasColumnName("value");
        builder.Property(e => e.OwnerId).HasColumnName("owner_id");
        builder.Property(e => e.ClientId).HasColumnName("client_id").IsRequired();
        builder.Property(e => e.ExpectedClose).HasColumnName("expected_close");
        builder.Property(e => e.ClosureScore).HasColumnName("closure_score");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasOne(e => e.Owner).WithMany().HasForeignKey(e => e.OwnerId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("fk_deal_owner");
        builder.HasOne(e => e.Client).WithMany(c => c.DealPropertyValues).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_deal_client");
    }
}
