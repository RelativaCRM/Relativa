using Microsoft.EntityFrameworkCore;
using Relativa.Persistence.Configurations;
using Relativa.Persistence.Entities;

namespace Relativa.Graph.Data;

public sealed class GraphDbContext(DbContextOptions<GraphDbContext> options) : DbContext(options)
{
    public DbSet<RabbitMqProcessedDelivery> RabbitMqProcessedDeliveries => Set<RabbitMqProcessedDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new RabbitMqProcessedDeliveryConfiguration());
    }
}
