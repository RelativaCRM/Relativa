using Relativa.Graph.Hubs;
using Relativa.Graph.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSignalR();

var graphConnectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(graphConnectionString))
{
    throw new InvalidOperationException("ConnectionStrings:Default must be configured for Graph choreography idempotency.");
}

builder.Services.AddNpgsqlDataSource(graphConnectionString);
builder.Services.Configure<RabbitMqGraphConsumerOptions>(
    builder.Configuration.GetSection(RabbitMqGraphConsumerOptions.SectionKey));
builder.Services.AddHostedService<DomainEventConsumerHostedService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Ok(new { service = "relativa-graph" }));

app.MapHub<GraphHub>("/hubs/graph");

app.Run();
