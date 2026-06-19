using Microsoft.EntityFrameworkCore;
using Relativa.Graph;
using Relativa.Graph.Dashboard;
using Relativa.Graph.Data;
using Relativa.Graph.Graph;
using Relativa.Graph.Hubs;
using Relativa.Graph.Messaging;
using Relativa.Graph.ML;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSignalR();

var graphConnectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(graphConnectionString))
{
    throw new InvalidOperationException("ConnectionStrings:Default must be configured for Graph choreography idempotency.");
}

builder.Services.AddDbContext<GraphDbContext>(opt => opt.UseNpgsql(graphConnectionString));
builder.Services.AddDbContext<GraphQueryDbContext>(opt => opt.UseNpgsql(graphConnectionString));
builder.Services.AddScoped<IGraphDataService, GraphDataService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IWorkspaceDashboardService, WorkspaceDashboardService>();

builder.Services.AddSingleton<IMlScoringClient, RabbitMqMlScoringClient>();
builder.Services.AddSingleton<IMlRecalculationClient, RabbitMqMlRecalculationClient>();

builder.Services.Configure<RabbitMqGraphConsumerOptions>(
    builder.Configuration.GetSection(RabbitMqGraphConsumerOptions.SectionKey));
builder.Services.AddHostedService<DomainEventConsumerHostedService>();

builder.Services.AddMemoryCache();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GraphGlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Ok(new { service = "relativa-graph" }));

EntityGraphEndpoints.MapEntityGraphEndpoints(app);
GraphQueryEndpoints.MapGraphQueryEndpoints(app);
DashboardEndpoints.MapDashboardEndpoints(app);
WorkspaceDashboardEndpoints.MapWorkspaceDashboardEndpoints(app);

app.MapHub<GraphHub>("/hubs/graph");

app.Run();

public partial class Program;
