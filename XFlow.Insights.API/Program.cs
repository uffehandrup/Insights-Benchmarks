using Marten;
using JasperFx.Events.Projections;
using JasperFx.Events.Daemon;
using JasperFx;
using EventStore.Client;
using Npgsql;
using XFlow.Insights.API.Domains.Workflows.Handlers;
using XFlow.Insights.API.Domains.Workflows;
using XFlow.Insights.API.Domains.Workflows.DomainEvents;
using XFlow.Insights.API.Domains.Workflows.Projections;
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
    builder.Services
        .AddMarten(opts =>
        {
            opts.Connection(connectionString);
            opts.Events.DatabaseSchemaName = "event_store";
            opts.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;

            // Async lifecycle gives us measurable projection catch-up latency.
            opts.Projections.Add<WorkflowDetailsProjection>(ProjectionLifecycle.Async);
        })
        .AddAsyncDaemon(DaemonMode.Solo);

    // 2. Register CQRS components
    builder.Services.AddScoped<IWorkflowRepository, WorkflowRepository>();
    builder.Services.AddScoped<IWorkflowQueryRepository, MartenWorkflowQueryRepository>();
}
else if (provider == "EventStore")
{
    connectionString = builder.Configuration["Database:EventStoreDBConnectionString"];
    var projectionConnectionString = builder.Configuration["Database:ProjectionPostgreSQLConnectionString"]
        ?? builder.Configuration["Database:PostgreSQLConnectionString"];

    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Database:EventStoreDBConnectionString is not configured in appsettings.");
    }
    if (string.IsNullOrEmpty(projectionConnectionString))
    {
        throw new InvalidOperationException("Database:ProjectionPostgreSQLConnectionString or Database:PostgreSQLConnectionString must be configured for EventStore projections.");
    }

    // Configure EventStoreDB client
    var settings = EventStoreClientSettings.Create(connectionString);
    var eventStoreClient = new EventStoreClient(settings);
    var projectionDataSource = NpgsqlDataSource.Create(projectionConnectionString);
    
    builder.Services.AddSingleton(eventStoreClient);
    builder.Services.AddSingleton(projectionDataSource);
    builder.Services.AddSingleton<PostgresProjectionSchemaManager>();
    builder.Services.AddSingleton<PostgresWorkflowDetailsStore>();
    builder.Services.AddSingleton<IWorkflowDetailsReadModelReader>(sp => sp.GetRequiredService<PostgresWorkflowDetailsStore>());
    builder.Services.AddSingleton<IWorkflowDetailsProjectionWriter>(sp => sp.GetRequiredService<PostgresWorkflowDetailsStore>());
    builder.Services.AddSingleton<IProjectionCheckpointStore, PostgresProjectionCheckpointStore>();
    builder.Services.AddSingleton<WorkflowDetailsProjector>();
    builder.Services.AddScoped<IWorkflowRepository, EventStoreDbWorkflowRepository>();
    builder.Services.AddScoped<IWorkflowQueryRepository, EventStoreDbWorkflowQueryRepository>();
    builder.Services.AddHostedService<EventStoreWorkflowProjectionWorker>();

    Console.WriteLine($"[Startup] EventStoreDB initialized");
    Console.WriteLine($"[Startup] Connection: {connectionString}");
    Console.WriteLine($"[Startup] Projection store: PostgreSQL");
}
else
{
    throw new InvalidOperationException($"Unknown database provider: {provider}. Expected 'Postgres' or 'EventStore'.");
}

builder.Services.AddScoped<WorkflowCommandHandler>();
builder.Services.AddScoped<WorkflowQueryHandler>();



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