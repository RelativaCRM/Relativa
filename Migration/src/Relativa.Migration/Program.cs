using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Relativa.Migration.Data;

// Default Host content root is the process working directory (e.g. Docker WORKDIR /repo),
// not the folder where appsettings.json is copied. Use the app base directory so
// appsettings.json next to the DLL is loaded (and env vars still override).
var builder = Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory);

builder.ConfigureServices((hostContext, services) =>
{
    var connectionString = hostContext.Configuration.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:Default is missing. Set it in appsettings.json or pass " +
            "ConnectionStrings__Default (e.g. Host=postgres;Port=5432;Database=...;Username=...;Password=...).");
    }

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
