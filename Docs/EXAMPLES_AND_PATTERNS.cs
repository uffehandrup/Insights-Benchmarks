// Example Usage and Before/After Comparisons

// ============================================================================
// BEFORE: Direct event handling (old approach)
// ============================================================================

// Old endpoint structure
/* 
group.MapPost("/started", async (WorkflowStartedEvent @event, IDocumentSession session) =>
{
    var streamId = @event.WorkflowId.ToString();
    session.Events.StartStream(streamId, @event);
    await session.SaveChangesAsync();
    return Results.Ok(new { StreamId = streamId, Message = "Event appended!" });
})

Issues:
- Business logic mixed in endpoint
- No validation before creating event
- Hard to reuse logic across endpoints
- No clear intent (what type of start?)
- Direct event creation (bypasses invariants)
*/

// ============================================================================
// AFTER: CQRS with aggregates (new approach)
// ============================================================================

using XFlow.Insights.API.Domains.Workflows.Commands;
using XFlow.Insights.API.Domains.Workflows.Handlers;
using XFlow.Insights.API.Domains.Workflows.Repositories;

// Endpoint (Clean and focused on routing)
group.MapPost("/start", async (StartWorkflowCommand cmd, IDocumentSession session, CancellationToken ct) =>
{
    try
    {
        var repository = new WorkflowRepository(session);
        var handler = new WorkflowCommandHandler(repository);
        var workflowId = await handler.HandleStartWorkflowAsync(cmd, ct);
        return Results.Created($"/api/workflows/{workflowId}", new { id = workflowId });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

/*
Benefits:
- Clear intent (StartWorkflowCommand)
- Validation in aggregate
- Reusable handler
- Can be called from multiple places (API, message handler, etc.)
- Testable in isolation
- Strong consistency guarantees
*/

// Command request
var startWorkflowRequest = new
{
    workflowId = 123,
    workflowName = "Process Invoice"
};

/*
POST /api/workflows/start
{
  "workflowId": 123,
  "workflowName": "Process Invoice"
}
*/

// ============================================================================
// EXAMPLE: MultiStep Workflow
// ============================================================================

// 1. Start workflow
var startCmd = new StartWorkflowCommand(
    WorkflowId: 123,
    WorkflowName: "Document Review Process"
);
await commandHandler.HandleStartWorkflowAsync(startCmd);

// Event raised: WorkflowStartedDomainEvent
// Projection updated: WorkflowDetails.CurrentStatus = "Running"

// 2. Complete first step
var step1Cmd = new CompleteWorkflowStepCommand(
    WorkflowId: 123,
    StepNumber: 1
);
await commandHandler.HandleCompleteStepAsync(step1Cmd);

// Event raised: WorkflowStepCompletedDomainEvent
// Projection updated: WorkflowDetails.StepNumber = 1

// 3. Check current state (read model)
var query = new GetWorkflowDetailsQuery(123);
var details = await queryHandler.HandleGetWorkflowDetailsAsync(query);
// Returns: { CurrentStatus: "Running", StepNumber: 1, StartedAt: ... }

// 4. Complete workflow
var completeCmd = new CompleteWorkflowCommand(
    WorkflowId: 123,
    FinalStatus: "Success"
);
await commandHandler.HandleCompleteWorkflowAsync(completeCmd);

// Event raised: WorkflowCompletedDomainEvent
// Projection updated: WorkflowDetails.CurrentStatus = "Success", CompletedAt = now

// ============================================================================
// EXAMPLE: Error Handling and Invariants
// ============================================================================

// Try to complete step on non-existent workflow
var cmd = new CompleteWorkflowStepCommand(WorkflowId: 999, StepNumber: 1);
await commandHandler.HandleCompleteStepAsync(cmd);
// Throws: InvalidOperationException("Workflow 999 not found")
// Caught by endpoint: Returns 400 BadRequest

// Try to fail an already completed workflow
await commandHandler.HandleCompleteWorkflowAsync(
    new CompleteWorkflowCommand(123, "Success")
);
await commandHandler.HandleFailWorkflowAsync(
    new FailWorkflowCommand(123, "Something went wrong")
);
// Throws: InvalidOperationException("Cannot fail workflow in Completed status")
// Invariant protected!

// ============================================================================
// EXAMPLE: Reading from Projections
// ============================================================================

// Query 1: Get workflow summary (fast, denormalized read)
var details = await queryHandler.HandleGetWorkflowDetailsAsync(
    new GetWorkflowDetailsQuery(123)
);

// Returns:
// {
//   Id: "123",
//   OriginalWorkflowId: 123,
//   CurrentStatus: "Running",
//   StartedAt: 2026-02-25T10:30:00Z,
//   LastUpdatedAt: 2026-02-25T10:35:00Z,
//   CompletedAt: null,
//   TotalEventsProcessed: 2,
//   StepNumber: 1
// }

// Query 2: Get complete event history (audit trail)
var eventHistory = await queryHandler.HandleGetWorkflowEventHistoryAsync(
    new GetWorkflowEventHistoryQuery(123)
);

// Returns:
// [
//   {
//     EventId: "GUID-1",
//     EventType: "WorkflowStartedDomainEvent",
//     OccurredAt: 2026-02-25T10:30:00Z,
//     EventData: "{...}",
//     Version: 1
//   },
//   {
//     EventId: "GUID-2",
//     EventType: "WorkflowStepCompletedDomainEvent",
//     OccurredAt: 2026-02-25T10:35:00Z,
//     EventData: "{...}",
//     Version: 2
//   }
// ]

// ============================================================================
// Dependency Graph (What calls what)
// ============================================================================

/*
Endpoint
  ├─> Command
  ├─> CommandHandler
  │    ├─> Repository
  │    │    └─> IDocumentSession (Marten)
  │    │         └─> Event Store (Database)
  │    └─> Aggregate
  │         ├─> Business Logic
  │         └─> Domain Events
  │
  └─> Query
       ├─> QueryHandler
       │    └─> IDocumentSession (Marten)
       │         └─> Read Model (Projection)
       └─> Results (DTOs)
*/

// ============================================================================
// Suggested DI Setup (Future Enhancement)
// ============================================================================

/*
In Program.cs, you could register handlers with DI to avoid manual instantiation:

// Register handlers
builder.Services.AddScoped<WorkflowCommandHandler>();
builder.Services.AddScoped<WorkflowQueryHandler>();
builder.Services.AddScoped<IWorkflowRepository, WorkflowRepository>();

Then in endpoint:
group.MapPost("/start", async (
    StartWorkflowCommand cmd,
    WorkflowCommandHandler handler,  // Auto-injected
    CancellationToken ct) =>
{
    var workflowId = await handler.HandleStartWorkflowAsync(cmd, ct);
    return Results.Created($"/api/workflows/{workflowId}", new { id = workflowId });
});

Or with a mediator pattern (MediatR):
group.MapPost("/start", async (
    StartWorkflowCommand cmd,
    IMediator mediator,
    CancellationToken ct) =>
{
    var result = await mediator.Send(cmd, ct);
    return Results.Created($"/api/workflows/{result}", new { id = result });
});
*/

// ============================================================================
// Testing Examples
// ============================================================================

namespace XFlow.Insights.API.Tests;

using Xunit;
using XFlow.Insights.API.Domains.Workflows.Aggregates;
using XFlow.Insights.API.Domains.Workflows.Commands;

public class WorkflowAggregateTests
{
    [Fact]
    public void CreateNew_ShouldRaiseStartedEvent()
    {
        // Arrange
        int workflowId = 123;
        string workflowName = "Test Workflow";

        // Act
        var aggregate = WorkflowAggregate.CreateNew(workflowId, workflowName);

        // Assert
        Assert.Single(aggregate.UncommittedEvents);
        var @event = aggregate.UncommittedEvents.First();
        Assert.IsType<WorkflowStartedDomainEvent>(@event);
    }

    [Fact]
    public void CompleteStep_WhenRunning_ShouldRaiseStepCompletedEvent()
    {
        // Arrange
        var aggregate = WorkflowAggregate.CreateNew(123, "Test");

        // Act
        aggregate.CompleteStep(1);

        // Assert
        Assert.Equal(2, aggregate.UncommittedEvents.Count); // Started + StepCompleted
        var lastEvent = aggregate.UncommittedEvents.Last();
        Assert.IsType<WorkflowStepCompletedDomainEvent>(lastEvent);
    }

    [Fact]
    public void CompleteStep_WhenCompleted_ShouldThrow()
    {
        // Arrange
        var aggregate = WorkflowAggregate.CreateNew(123, "Test");
        aggregate.Complete();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => aggregate.CompleteStep(2));
    }

    [Fact]
    public void FromHistory_ShouldReconstructState()
    {
        // Arrange
        var events = new List<DomainEvent>
        {
            new WorkflowStartedDomainEvent(123, "Test", DateTime.UtcNow) { Version = 1 },
            new WorkflowStepCompletedDomainEvent(123, 1, DateTime.UtcNow) { Version = 2 }
        };

        // Act
        var aggregate = WorkflowAggregate.FromHistory(events);

        // Assert
        Assert.Equal(123, aggregate.WorkflowId);
        Assert.Equal("Test", aggregate.WorkflowName);
        Assert.Equal(1, aggregate.CurrentStep);
        Assert.Equal(WorkflowStatus.Running, aggregate.Status);
    }
}

// ============================================================================
// Migration Checklist
// ============================================================================

/*
□ Update all endpoints to use commands instead of events
□ Create command handlers for all use cases
□ Update projections to handle new domain events
□ Add aggregate validation rules
□ Create unit tests for aggregates
□ Create integration tests for handlers
□ Update API documentation
□ Add error handling and logging
□ Consider idempotency for command handlers
□ Add metrics/monitoring for events
□ Backwards compatibility for existing events (if migrating live system)
□ Database migration to handle schema changes
*/
