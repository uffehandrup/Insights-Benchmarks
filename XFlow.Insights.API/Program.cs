using Marten;
using JasperFx.Events.Projections;
using JasperFx;
using EventStore.Client;
using XFlow.Insights.API.Domains.Workflows;
using XFlow.Insights.API.Domains.Workflows.DomainEvents;
using XFlow.Insights.API.Domains.Workflows.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add OpenAPI/Swagger support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = "";
// 1. Configure database provider
var provider = builder.Configuration["Database:Provider"];

if (provider == "Postgres")
{
    connectionString = builder.Configuration["Database:PostgreSQLConnectionString"];
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Database:PostgreSQLConnectionString is not configured in appsettings.");
    }
    builder.Services.AddMarten(opts =>
    {
    opts.Connection(connectionString);
    opts.Events.DatabaseSchemaName = "event_store";
    opts.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;

    // Register the projection to handle domain events
    // Inline means the read model is updated in the same transaction as the event append.
    // Guarantees strong consistency.
    opts.Projections.Add<WorkflowDetailsProjection>(ProjectionLifecycle.Inline);
    });

    // 2. Register CQRS components
    builder.Services.AddScoped<IWorkflowRepository, WorkflowRepository>();
}
else if (provider == "EventStore")
{
    connectionString = builder.Configuration["Database:EventStoreDBConnectionString"];
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Database:EventStoreDBConnectionString is not configured in appsettings.");
    }

    // Configure EventStoreDB client
    var settings = EventStoreClientSettings.Create(connectionString);
    var eventStoreClient = new EventStoreClient(settings);
    
    builder.Services.AddSingleton(eventStoreClient);
    builder.Services.AddScoped<IWorkflowRepository, EventStoreDbWorkflowRepository>();

    Console.WriteLine($"[Startup] EventStoreDB initialized");
    Console.WriteLine($"[Startup] Connection: {connectionString}");
}
else
{
    throw new InvalidOperationException($"Unknown database provider: {provider}. Expected 'Postgres' or 'EventStore'.");
}



var app = builder.Build();

// Enable Swagger UI in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Map domain endpoints
app.MapWorkflowEndpoints();

app.Run();