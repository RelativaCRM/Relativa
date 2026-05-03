using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public sealed class RabbitMqProcessedDeliveryConfiguration : IEntityTypeConfiguration<RabbitMqProcessedDelivery>
{
    public void Configure(EntityTypeBuilder<RabbitMqProcessedDelivery> builder)
    {
        builder.ToTable("rabbitmq_processed_delivery");
        builder.HasKey(x => new { x.MessageId, x.ConsumerGroup });

        builder.Property(x => x.MessageId)
            .HasColumnName("message_id")
            .IsRequired();

        builder.Property(x => x.ConsumerGroup)
            .HasColumnName("consumer_group")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ProcessedAtUtc)
            .HasColumnName("processed_at_utc")
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
