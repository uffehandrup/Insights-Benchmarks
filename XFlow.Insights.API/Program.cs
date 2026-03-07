using Marten;
using JasperFx.Events.Projections;
using JasperFx;
using XFlow.Insights.API.Domains.Workflows;
using XFlow.Insights.API.Domains.Workflows.DomainEvents;
using XFlow.Insights.API.Domains.Workflows.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add OpenAPI/Swagger support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 1. Configure Marten
var connectionString = builder.Configuration["Database:ConnectionString"];
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Database:ConnectionString is not configured in appsettings.");
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