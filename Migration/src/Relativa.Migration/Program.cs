using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Relativa.Migration.Data;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((hostContext, services) =>
{
    var connectionString = hostContext.Configuration.GetConnectionString("Default");

    services.AddDbContext<MigrationDbContext>(options =>
        options.UseNpgsql(connectionString, b => b.MigrationsAssembly("Relativa.Migration")));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();
    try
    {
        Console.WriteLine("Applying migrations to the database...");
        await dbContext.Database.MigrateAsync();
        Console.WriteLine("Migrations applied successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Critical error applying migrations: {ex.Message}");
        throw;
    }
}
